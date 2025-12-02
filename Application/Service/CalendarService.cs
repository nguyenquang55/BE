using Application.Abstractions.Infrastructure;
using Application.Abstractions.Services;
using Application.Contracts.Contact;
using Application.Contracts.ThirdParty.Calendar.Request;
using Application.Contracts.ThirdParty.Calendar.Respone;
using Application.Model;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Org.BouncyCastle.Ocsp;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Application.Service
{
    public class CalendarService : ICalendarService
    {
        private static string CacheKey(Guid userId) => $"Contacts:{userId}";
        private readonly IRedisCacheService _redisCacheService;
        private readonly IGeminiClient _geminiClient;
        private readonly IOAuthTokenService _oauthTokenService;
        private readonly HttpClient _httpClient;

        public CalendarService(IRedisCacheService redisCacheService, IGeminiClient geminiClient, HttpClient httpClient, IOAuthTokenService oauthTokenService)
        {
            _httpClient = httpClient;
            _geminiClient = geminiClient;
            _redisCacheService = redisCacheService;
            _oauthTokenService = oauthTokenService;
        }

        #region Main Fucntions
        public async Task<Result<CreateEventRespone>> CreateEvent(MberModelRespone modelRespone, Guid userId)
        {
            var previewRes = await BuildCreatePreviewAsync(modelRespone, userId);
            if (!previewRes.Success || previewRes.Data?.ExecutionPayload == null)
                return Result<CreateEventRespone>.FailureResult(previewRes.Message ?? "Cannot create event preview.");

            var execPayloadObj = previewRes.Data.ExecutionPayload as CreateEventExecutionPayload;
            if (execPayloadObj == null)
            {
                try
                {
                    var json = JsonSerializer.Serialize(previewRes.Data.ExecutionPayload);
                    execPayloadObj = JsonSerializer.Deserialize<CreateEventExecutionPayload>(json);
                }
                catch { }
            }

            if (execPayloadObj == null)
                return Result<CreateEventRespone>.FailureResult("Invalid execution payload.");

            return await ExecuteCreateAsync(execPayloadObj, userId);
        }

        public Task<Result<DeleteEventRespone>> DeleteEvent(MberModelRespone modelRespone, Guid userId)
        {
            var previewRes = BuildDeletePreviewAsync(modelRespone, userId);
            if (previewRes == null || !previewRes.Result.Success)
            {
                return Task.FromResult(Result<DeleteEventRespone>.FailureResult(previewRes?.Result.Message ?? "Cannot build delete preview."));
            }
            var previewList = previewRes.Result.Data;
            if (previewList == null || previewList.Count == 0)
            {
                return Task.FromResult(Result<DeleteEventRespone>.FailureResult("No matching events found for deletion."));
            }
            foreach (var preview in previewList)
            {
                if (preview.ExecutionPayload == null)
                {
                    return Task.FromResult(Result<DeleteEventRespone>.FailureResult("Execution payload missing in preview."));
                }
                var execPayloadObj = preview.ExecutionPayload as DeleteEventExecutionPayload;
                if (execPayloadObj == null)
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(preview.ExecutionPayload);
                        execPayloadObj = JsonSerializer.Deserialize<DeleteEventExecutionPayload>(json);
                    }
                    catch
                    {
                        return Task.FromResult(Result<DeleteEventRespone>.FailureResult("Invalid execution payload format."));
                    }
                }
                if (execPayloadObj == null)
                {
                    return Task.FromResult(Result<DeleteEventRespone>.FailureResult("Invalid execution payload."));
                }
                return ExecuteDeleteAsync(execPayloadObj, userId);
            }
            return Task.FromResult(Result<DeleteEventRespone>.FailureResult("No events to delete."));
        }

        public async Task<Result<SearchEventLLMRespone>> SearchEvents(MberModelRespone modelRespone, Guid userId)
        {
            var finalDisplayResult = await SearchEventsHelper(modelRespone, userId);
            if (!finalDisplayResult.Success)
            {
                return Result<SearchEventLLMRespone>.FailureResult(finalDisplayResult.Message ?? "Failed to search events.");
            }
            var finalDisplayList = finalDisplayResult.Data;

            var sb = new StringBuilder();
            foreach (var e in finalDisplayList)
            {
                sb.AppendLine($"- {e.Title} ({e.StartTime:HH:mm} - {e.EndTime:HH:mm, dd/MM})");
            }
            string eventsTextList = sb.ToString();

            bool isVi = IsVietnamese(modelRespone.InputText);
            string composePrompt = isVi
                ? $@"Người dùng hỏi: ""{modelRespone.InputText}""
                 Danh sách sự kiện tìm được:
                 {eventsTextList}
                 
                 Hãy trả lời tự nhiên bằng tiếng Việt.
                 - Nếu có sự kiện, liệt kê ngắn gọn (Giờ + Tên).
                 - Nếu danh sách rỗng, báo không tìm thấy thông tin sự kiện thật là ngắn gọn cho tôi.
                 - Không bịa đặt thông tin."
                : $@"User asked: ""{modelRespone.InputText}""
                 Found events:
                 {eventsTextList}
                 
                 Answer naturally in English. Summarize the events.";

            var finalAnswer = await _geminiClient.CallGemini(composePrompt);

            return Result<SearchEventLLMRespone>.SuccessResult(new SearchEventLLMRespone
            {
                Results = finalAnswer?.ToString()?.Trim() ?? "No response generated."
            });
        }

        public Task<Result<UpdateEventRespone>> UpdateEvent(MberModelRespone modelRespone, Guid userId)
        {
            var previewRes = BuildUpdatePreviewAsync(modelRespone, userId);
            if (previewRes == null || !previewRes.Result.Success)
            {
                return Task.FromResult(Result<UpdateEventRespone>.FailureResult(previewRes?.Result.Message ?? "Cannot build update preview."));
            }
            var previewList = previewRes.Result.Data;
            if(previewList == null || previewList.Count == 0)
            {
                return Task.FromResult(Result<UpdateEventRespone>.FailureResult("No matching events found for update."));
            }
            foreach (var preview in previewList)
            {
                if (preview.ExecutionPayload == null)
                {
                    return Task.FromResult(Result<UpdateEventRespone>.FailureResult("Execution payload missing in preview."));
                }
                var execPayloadObj = preview.ExecutionPayload as UpdateEventExecutionPayload;
                if (execPayloadObj == null)
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(preview.ExecutionPayload);
                        execPayloadObj = JsonSerializer.Deserialize<UpdateEventExecutionPayload>(json);
                    }
                    catch
                    {
                        return Task.FromResult(Result<UpdateEventRespone>.FailureResult("Invalid execution payload format."));
                    }
                }
                if (execPayloadObj == null)
                {
                    return Task.FromResult(Result<UpdateEventRespone>.FailureResult("Invalid execution payload."));
                }
                return ExecuteUpdateAsync(execPayloadObj, userId);
            }
            return Task.FromResult(Result<UpdateEventRespone>.FailureResult("No events to update."));
        }

        public async Task<Result<List<CalendarOperationPreview>>> BuildUpdatePreviewAsync(MberModelRespone modelRespone, Guid userId)
        {
            var candidatesRes = await SearchEventsHelper(modelRespone, userId);
            if (!candidatesRes.Success || candidatesRes.Data == null || candidatesRes.Data.Count == 0)
            {
                return Result<List<CalendarOperationPreview>>.FailureResult("Can not find matching events");
            }

            var list = candidatesRes.Data;
            var preview = new List<CalendarOperationPreview>();

            foreach (var item in list)
            {
                var selection = new CalendarOperationPreview
                {
                    Action = "update",
                    Title = item.Title,
                    Start = item.StartTime,
                    End = item.EndTime,
                    TargetEventId = item.Id
                };
                preview.Add(selection);
            }
            return Result<List<CalendarOperationPreview>>.SuccessResult(preview);

        }

        public async Task<Result<List<CalendarOperationPreview>>> BuildDeletePreviewAsync(MberModelRespone modelRespone, Guid userId)
        {
            var candidatesRes = await SearchEventsHelper(modelRespone, userId);
            if (!candidatesRes.Success || candidatesRes.Data == null || candidatesRes.Data.Count == 0)
            {
                return Result<List<CalendarOperationPreview>>.FailureResult("Can not find matching events");
            }

            var list = candidatesRes.Data;
            var preview = new List<CalendarOperationPreview>();

            foreach (var item in list)
            {
                var selection = new CalendarOperationPreview
                {
                    Action = "delete",
                    Title = item.Title,
                    Start = item.StartTime,
                    End = item.EndTime,
                    TargetEventId= item.Id
                };
                preview.Add(selection);
            }
            return Result<List<CalendarOperationPreview>>.SuccessResult(preview);
        }

        public async Task<Result<UpdateEventRespone>> ExecuteUpdateAsync(UpdateEventExecutionPayload payload, Guid userId)
        {
            string accessToken = await _oauthTokenService.GetAccessToken(userId);
            if (string.IsNullOrEmpty(accessToken))
                return Result<UpdateEventRespone>.FailureResult("Access token not found.");

            try
            {
                var googleService = new Google.Apis.Calendar.v3.CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken(accessToken),
                    ApplicationName = "CalendarOAuthDemo"
                });

                // Fetch existing event
                var getReq = googleService.Events.Get("primary", payload.EventId);
                var existing = await getReq.ExecuteAsync();
                if (existing == null)
                    return Result<UpdateEventRespone>.FailureResult("Event not found.");

                if (!string.IsNullOrWhiteSpace(payload.NewTitle))
                    existing.Summary = payload.NewTitle;
                if (payload.NewStart.HasValue)
                    existing.Start = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(payload.NewStart.Value.ToUniversalTime()) };
                if (payload.NewEnd.HasValue)
                    existing.End = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(payload.NewEnd.Value.ToUniversalTime()) };

                var updateReq = googleService.Events.Update(existing, "primary", payload.EventId);
                // updateReq.IfMatch = existing.ETag;
                var updated = await updateReq.ExecuteAsync();
                if (updated != null)
                    return Result<UpdateEventRespone>.SuccessResult(new UpdateEventRespone { IsUpdated = true });

                return Result<UpdateEventRespone>.FailureResult("Unable to update event on Google Calendar.");
            }
            catch (Exception ex)
            {
                return Result<UpdateEventRespone>.FailureResult($"Error updating Google Calendar event: {ex.Message}");
            }
        }

        public async Task<Result<DeleteEventRespone>> ExecuteDeleteAsync(List<DeleteEventExecutionPayload> payload, Guid userId)
        {
            string accessToken = await _oauthTokenService.GetAccessToken(userId);
            if (string.IsNullOrEmpty(accessToken))
                return Result<DeleteEventRespone>.FailureResult("Access token not found.");

            try
            {
                var googleService = new Google.Apis.Calendar.v3.CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken(accessToken),
                    ApplicationName = "CalendarOAuthDemo"
                });

                bool allDeleted = true;
                foreach (var payloadid in payload)
                {
                    try
                    {
                        var delReq = googleService.Events.Delete("primary", payloadid.EventId);
                        await delReq.ExecuteAsync();
                    }
                    catch
                    {
                        allDeleted = false;
                    }
                }
                if (allDeleted)
                    return Result<DeleteEventRespone>.SuccessResult(new DeleteEventRespone { IsDeleted = true });
                else
                    return Result<DeleteEventRespone>.FailureResult("Some events could not be deleted.");
            }
            catch (Exception ex)
            {
                return Result<DeleteEventRespone>.FailureResult($"Error deleting Google Calendar events: {ex.Message}");
            }
        }

        public async Task<Result<CalendarOperationPreview>> BuildCreatePreviewAsync(MberModelRespone modelRespone, Guid userId)
        {
            string prompt = $@"hãy phân tích câu ""{modelRespone.InputText}"" theo mẫu json sau:
                            {{
                                ""Title"": null,
                                ""StartDateTime"": null,
                                ""EndDateTime"": null
                            }}
                            nếu thiếu trường nào đó thì trả về dạng json như trên và để null giá trị đó, giá trị Date phải ghi rõ ngày đó là ngày nào với ngày hôm nay là {DateTime.Now:dd/MM/yyyy} và không giải thích gì thêm";

            var llmResponse = await _geminiClient.CallGemini(prompt);
            var jsonString = llmResponse?.ToString()?.Replace("```json", "").Replace("```", "").Trim();
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                return Result<CalendarOperationPreview>.FailureResult("Unable to parse event information from input.");
            }

            CreateEventRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<CreateEventRequest>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return Result<CalendarOperationPreview>.FailureResult("Unable to parse event information from input.");
            }

            if (req == null)
                return Result<CalendarOperationPreview>.FailureResult("Event information is incomplete.");

            var warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(req.Title)) warnings.Add("Missing event title");
            if (string.IsNullOrWhiteSpace(req.StartDateTime) && string.IsNullOrWhiteSpace(req.EndDateTime)) warnings.Add("Missing start/end time");

            // Parse times
            DateTime startDt = default, endDt = default; bool hasStart = false, hasEnd = false;
            string[] parseFormats = new[]
            {
                "dd/MM/yyyy HH:mm","d/M/yyyy H:mm","dd/MM/yyyy H:mm","d/M/yyyy HH:mm",
                "yyyy-MM-dd HH:mm","yyyy-M-d H:mm","yyyy-MM-ddTHH:mm:ss","yyyy-MM-ddTHH:mm:ssZ",
                "dd/MM/yyyy HH:mm:ss","d/M/yyyy HH:mm:ss"
            };

            try
            {
                if (!string.IsNullOrWhiteSpace(req.StartDateTime))
                {
                    var s = req.StartDateTime.Trim();
                    if (DateTime.TryParseExact(s, parseFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var tmpStart) ||
                        DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out tmpStart))
                    {
                        startDt = DateTime.SpecifyKind(tmpStart, DateTimeKind.Local);
                        hasStart = true;
                    }
                    else warnings.Add($"Cannot parse StartDateTime: '{req.StartDateTime}'");
                }

                if (!string.IsNullOrWhiteSpace(req.EndDateTime))
                {
                    var s = req.EndDateTime.Trim();
                    if (DateTime.TryParseExact(s, parseFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var tmpEnd) ||
                        DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out tmpEnd))
                    {
                        endDt = DateTime.SpecifyKind(tmpEnd, DateTimeKind.Local);
                        hasEnd = true;
                    }
                    else warnings.Add($"Cannot parse EndDateTime: '{req.EndDateTime}'");
                }

                if (!hasStart && hasEnd) { startDt = endDt.AddHours(-1); hasStart = true; }
                if (hasStart && !hasEnd) { endDt = startDt.AddHours(1); hasEnd = true; }
            }
            catch { }

            var preview = new CalendarOperationPreview
            {
                Action = "create",
                Title = req.Title,
                Start = hasStart ? startDt : null,
                End = hasEnd ? endDt : null,
                Warnings = warnings,
                ExecutionPayload = (hasStart && hasEnd && !string.IsNullOrWhiteSpace(req.Title))
                    ? new CreateEventExecutionPayload { Title = req.Title!, Start = startDt, End = endDt }
                    : null,
                ConfidenceScore = modelRespone?.ConfidenceScore
            };

            return Result<CalendarOperationPreview>.SuccessResult(preview);
        }

        public async Task<Result<CreateEventRespone>> ExecuteCreateAsync(CreateEventExecutionPayload payload, Guid userId)
        {
            string accessToken = await _oauthTokenService.GetAccessToken(userId);
            if (string.IsNullOrEmpty(accessToken))
                return Result<CreateEventRespone>.FailureResult("Access token not found.");

            try
            {
                var googleService = new Google.Apis.Calendar.v3.CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken(accessToken),
                    ApplicationName = "CalendarOAuthDemo"
                });

                var startDateTime = payload.Start.ToUniversalTime();
                var endDateTime = payload.End.ToUniversalTime();

                var newEvent = new Event
                {
                    Summary = payload.Title,
                    Start = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(startDateTime) },
                    End = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(endDateTime) }
                };

                var insertRequest = googleService.Events.Insert(newEvent, "primary");
                var createdEvent = await insertRequest.ExecuteAsync();
                if (createdEvent != null && !string.IsNullOrEmpty(createdEvent.Id))
                    return Result<CreateEventRespone>.SuccessResult(new CreateEventRespone { IsCreated = true });

                return Result<CreateEventRespone>.FailureResult("Unable to create event on Google Calendar.");
            }
            catch (Exception ex)
            {
                return Result<CreateEventRespone>.FailureResult($"Error creating Google Calendar event: {ex.Message}");
            }
        }
        #endregion

        #region Helpers
        private async Task<Result<List<SearchEventRespone>>> SearchEventsHelper(MberModelRespone modelRespone, Guid userId)
        {
            string accessToken = await _oauthTokenService.GetAccessToken(userId);
            if (string.IsNullOrEmpty(accessToken))
            {
                return Result<List<SearchEventRespone>>.FailureResult("Access token not found.");
            }
            TimeZoneInfo userTimeZone;
            try
            {
                userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }

            DateTime userNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone);

            string extractionPrompt = $@"
                Input: ""{modelRespone.InputText}""
                Context: Today is {userNow:dd/MM/yyyy (dddd)}.
                Task: Extract search intent into JSON.
                Format:
                {{
                    ""Title"": string|null (keywords to search),
                    ""StartDateTime"": string|null (dd/MM/yyyy HH:mm),
                    ""EndDateTime"": string|null (dd/MM/yyyy HH:mm)
                }}
                Rules:
                - If user says 'tomorrow', calculate based on Today ({userNow:dd/MM/yyyy}).
                - If user says 'sáng nay', 'chiều nay', infer specific hours if possible.
                - Return ONLY JSON string, no markdown.";

            var llmResponse = await _geminiClient.CallGemini(extractionPrompt);
            var jsonString = llmResponse?.ToString()?.Replace("```json", "").Replace("```", "").Trim();

            SearchEventRequest? calendarIntent;
            try
            {
                calendarIntent = JsonSerializer.Deserialize<SearchEventRequest>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                calendarIntent = new SearchEventRequest { Title = modelRespone.InputText };
            }

            if (calendarIntent == null) return Result<List<SearchEventRespone>>.FailureResult("Unable to understand search intent.");

            DateTime startUserTime;
            DateTime endUserTime;

            DateTime? parsedStart = ParseUserDate(calendarIntent.StartDateTime, userNow);
            DateTime? parsedEnd = ParseUserDate(calendarIntent.EndDateTime, userNow);

            if (parsedStart.HasValue && parsedEnd.HasValue)
            {
                startUserTime = parsedStart.Value;
                endUserTime = parsedEnd.Value;
            }
            else if (parsedStart.HasValue)
            {
                startUserTime = parsedStart.Value;
                endUserTime = startUserTime.Date.AddDays(1).AddTicks(-1);
            }
            else
            {
                startUserTime = userNow.Date;
                endUserTime = userNow.Date.AddDays(1).AddTicks(-1);
            }

            if (startUserTime > endUserTime) endUserTime = startUserTime.AddHours(1);

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startUserTime, userTimeZone);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(endUserTime, userTimeZone);

            var googleService = new Google.Apis.Calendar.v3.CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken(accessToken),
                ApplicationName = "CalendarApp"
            });

            var allEvents = new List<SearchEventRespone>();
            string? pageToken = null;

            try
            {
                do
                {
                    var request = googleService.Events.List("primary");
                    request.ShowDeleted = false;
                    request.SingleEvents = true;
                    request.MaxResults = 50;
                    request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
                    request.TimeMinDateTimeOffset = new DateTimeOffset(startUtc);
                    request.TimeMaxDateTimeOffset = new DateTimeOffset(endUtc);
                    request.Fields = "items(id,summary,start,end),nextPageToken";
                    request.PageToken = pageToken;

                    var events = await request.ExecuteAsync();

                    if (events.Items != null)
                    {
                        foreach (var item in events.Items)
                        {
                            var itemStartVal = item.Start.DateTimeDateTimeOffset ?? DateTimeOffset.Parse(item.Start.Date);
                            var itemEndVal = item.End.DateTimeDateTimeOffset ?? DateTimeOffset.Parse(item.End.Date);

                            var itemStartUser = TimeZoneInfo.ConvertTime(itemStartVal, userTimeZone).DateTime;
                            var itemEndUser = TimeZoneInfo.ConvertTime(itemEndVal, userTimeZone).DateTime;

                            allEvents.Add(new SearchEventRespone
                            {
                                Id = item.Id,
                                Title = item.Summary ?? "(No Title)",
                                StartTime = itemStartUser,
                                EndTime = itemEndUser
                            });
                        }
                    }
                    pageToken = events.NextPageToken;

                } while (!string.IsNullOrEmpty(pageToken));
            }
            catch (Exception ex)
            {
                return Result<List<SearchEventRespone>>.FailureResult($"Google API Error: {ex.Message}");
            }

            // Filtering logic
            var filteredEvents = allEvents;
            string intentTitle = calendarIntent.Title?.Trim();

            if (!string.IsNullOrEmpty(intentTitle))
            {
                var candidates = allEvents.Select(e => new { e.Id, e.Title, Time = e.StartTime.ToString("dd/MM HH:mm") }).ToList();
                string candidatesJson = JsonSerializer.Serialize(candidates);

                string filterPrompt = $@"
                Find events matching intent: ""{intentTitle}""
                Candidates: {candidatesJson}
                Return JSON array of IDs only. Example: [""id1"", ""id2""]. If none, return [].";

                try
                {
                    var filterRes = await _geminiClient.CallGemini(filterPrompt);
                    var filterIds = JsonSerializer.Deserialize<List<string>>(CleanJson(filterRes.ToString()));
                    if (filterIds != null && filterIds.Any())
                    {
                        filteredEvents = allEvents.Where(e => filterIds.Contains(e.Id)).ToList();
                    }
                    else
                    {
                        var simpleFilter = allEvents.Where(e => e.Title.Contains(intentTitle, StringComparison.OrdinalIgnoreCase)).ToList();
                        filteredEvents = simpleFilter;
                    }
                }
                catch { }
            }
            return Result<List<SearchEventRespone>>.SuccessResult(filteredEvents);
        }

        private DateTime? ParseUserDate(string? dateStr, DateTime userNow)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return null;
            dateStr = dateStr.Trim();

            var formats = new[] { "dd/MM/yyyy HH:mm", "dd/MM/yyyy", "yyyy-MM-dd HH:mm", "yyyy-MM-dd" };
            if (DateTime.TryParseExact(dateStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt;
            }
            return null;
        }

        private string CleanJson(string input)
        {
            if (string.IsNullOrEmpty(input)) return "[]";
            return input.Replace("```json", "").Replace("```", "").Trim();
        }

        private bool IsVietnamese(string? text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string viChars = "ăâđêôơưàáảãạằắẳẵặầấẩẫậèéẻẽẹềếểễệìíỉĩịòóỏõọồốổỗộờớởỡợùúủũụừứửữựỳýỷỹỵ";
            return text.ToLower().IndexOfAny(viChars.ToCharArray()) >= 0;
        }
        #endregion
    }
}
