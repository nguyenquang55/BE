using Application.Abstractions.Infrastructure;
using Application.Abstractions.Services;
using Application.Contracts.Contact;
using Application.Contracts.ThirdParty.Calendar.Request;
using Application.Contracts.ThirdParty.Calendar.Respone;
using Application.Model;
using Shared.Common;
using Google.Apis.Calendar.v3.Data;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Services;
using Google.Apis.Calendar.v3;
using System.Collections.Generic;
using System.Linq;

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
            throw new NotImplementedException();
        }

        public async Task<Result<List<SearchEventRespone>>> SearchEvents(MberModelRespone modelRespone, Guid userId)
        {
            string accessToken = await _oauthTokenService.GetAccessToken(userId);
            if (string.IsNullOrEmpty(accessToken))
            {
                return Result<List<SearchEventRespone>>.FailureResult("Access token not found.");
            }

            string prompt = $@"hãy phân tích câu ""{modelRespone.InputText}"" theo mẫu json sau:
                    {{
                        ""Title"": null,
                        ""StartDateTime"": null,
                        ""EndDateTime"": null
                    }}
                    nếu thiếu trường nào đó thì trả về dạng json như trên và để null giá trị đó, giá trị Date phải ghi rõ ngày đó là ngày nào với ngày hôm nay là {DateTime.Now:dd/MM/yyyy} và không giải thích gì thêm";

            var llmResponse = await _geminiClient.CallGemini(prompt);
            var jsonString = llmResponse?.ToString()?.Replace("```json", "").Replace("```", "").Trim();

            SearchEventRequest? calendar;
            try
            {
                calendar = JsonSerializer.Deserialize<SearchEventRequest>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return Result<List<SearchEventRespone>>.FailureResult("Unable to parse event information.");
            }

            if (calendar == null) return Result<List<SearchEventRespone>>.FailureResult("Event information is incomplete.");

            var googleService = new Google.Apis.Calendar.v3.CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken(accessToken),
                ApplicationName = "CalendarOAuthDemo"
            });

            try
            {
                EventsResource.ListRequest request = googleService.Events.List("primary");
                request.ShowDeleted = false;
                request.SingleEvents = true;
                request.MaxResults = 50; // fetch more items per page
                request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

                DateTime? startLocal = null;
                DateTime? endLocal = null;

                var formats = new[]
                {
                    "dd/MM/yyyy HH:mm","d/M/yyyy H:mm","dd/MM/yyyy H:mm","d/M/yyyy HH:mm",
                    "dd/MM/yyyy","d/M/yyyy",
                    "yyyy-MM-dd HH:mm","yyyy-M-d H:mm","yyyy-MM-ddTHH:mm:ss","yyyy-MM-ddTHH:mm:ssZ",
                    "yyyy-MM-dd"
                };

                var vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
                var inv = System.Globalization.CultureInfo.InvariantCulture;

                if (!string.IsNullOrWhiteSpace(calendar.StartDateTime))
                {
                    var s = calendar.StartDateTime.Trim();
                    if (DateTime.TryParseExact(s, formats, inv, System.Globalization.DateTimeStyles.AssumeLocal, out var tmp) ||
                        DateTime.TryParse(s, vi, System.Globalization.DateTimeStyles.AssumeLocal, out tmp) ||
                        DateTime.TryParse(s, inv, System.Globalization.DateTimeStyles.AssumeLocal, out tmp))
                    {
                        startLocal = DateTime.SpecifyKind(tmp, DateTimeKind.Local);
                    }
                }

                if (!string.IsNullOrWhiteSpace(calendar.EndDateTime))
                {
                    var s = calendar.EndDateTime.Trim();
                    if (DateTime.TryParseExact(s, formats, inv, System.Globalization.DateTimeStyles.AssumeLocal, out var tmp) ||
                        DateTime.TryParse(s, vi, System.Globalization.DateTimeStyles.AssumeLocal, out tmp) ||
                        DateTime.TryParse(s, inv, System.Globalization.DateTimeStyles.AssumeLocal, out tmp))
                    {
                        endLocal = DateTime.SpecifyKind(tmp, DateTimeKind.Local);
                    }
                }

                if (startLocal.HasValue && !endLocal.HasValue)
                {
                    endLocal = startLocal.Value.Date.AddDays(1).AddTicks(-1);
                    startLocal = startLocal.Value.Date;
                }
                else if (!startLocal.HasValue && endLocal.HasValue)
                {
                    startLocal = endLocal.Value.Date;
                    endLocal = endLocal.Value.Date.AddDays(1).AddTicks(-1);
                }
                else if (!startLocal.HasValue && !endLocal.HasValue)
                {
                    // Infer relative dates from natural language when LLM omits dates
                    var input = modelRespone?.InputText?.ToLowerInvariant() ?? string.Empty;
                    var today = DateTime.Now.Date;
                    var tomorrow = today.AddDays(1);

                    if (input.Contains("ngày mai") || input.Contains("tomorrow") || input.Contains("mai"))
                    {
                        startLocal = tomorrow;
                        endLocal = tomorrow.AddDays(1).AddTicks(-1);
                    }
                    else if (input.Contains("hôm nay") || input.Contains("today"))
                    {
                        startLocal = today;
                        endLocal = today.AddDays(1).AddTicks(-1);
                    }
                    else
                    {
                        // Default to today
                        startLocal = today;
                        endLocal = today.AddDays(1).AddTicks(-1);
                    }
                }

                if (startLocal.HasValue)
                    request.TimeMinDateTimeOffset = new DateTimeOffset(startLocal.Value.ToUniversalTime());
                if (endLocal.HasValue)
                    request.TimeMaxDateTimeOffset = new DateTimeOffset(endLocal.Value.ToUniversalTime());

                var potentialEvents = new List<SearchEventRespone>();
                string? pageToken = null;
                do
                {
                    request.PageToken = pageToken;
                    Events events = await request.ExecuteAsync();

                    if (events.Items != null && events.Items.Count > 0)
                    {
                        foreach (var eventItem in events.Items)
                        {
                        DateTime startTimeLocal = DateTime.MinValue;
                        if (!string.IsNullOrEmpty(eventItem.Start?.Date) && DateTime.TryParse(eventItem.Start.Date, inv, System.Globalization.DateTimeStyles.AssumeLocal, out var d))
                            startTimeLocal = DateTime.SpecifyKind(d.Date, DateTimeKind.Local);
                        else if (eventItem.Start?.DateTimeDateTimeOffset != null)
                            startTimeLocal = eventItem.Start.DateTimeDateTimeOffset.Value.LocalDateTime;

                        DateTime endTimeLocal = DateTime.MinValue;
                        if (!string.IsNullOrEmpty(eventItem.End?.Date) && DateTime.TryParse(eventItem.End.Date, inv, System.Globalization.DateTimeStyles.AssumeLocal, out var d2))
                            endTimeLocal = DateTime.SpecifyKind(d2.Date, DateTimeKind.Local);
                        else if (eventItem.End?.DateTimeDateTimeOffset != null)
                            endTimeLocal = eventItem.End.DateTimeDateTimeOffset.Value.LocalDateTime;

                        bool inRange = true;
                        if (startLocal.HasValue && endLocal.HasValue)
                            inRange = startTimeLocal != DateTime.MinValue && startTimeLocal >= startLocal.Value && startTimeLocal <= endLocal.Value;
                        else if (startLocal.HasValue)
                            inRange = startTimeLocal != DateTime.MinValue && startTimeLocal.Date == startLocal.Value.Date;

                        if (!inRange) continue;
                            potentialEvents.Add(new SearchEventRespone
                            {
                                Id = eventItem.Id,
                                Title = eventItem.Summary,
                                StartTime = startTimeLocal,
                                EndTime = endTimeLocal,
                            });
                        }
                    }

                    pageToken = events.NextPageToken;
                } while (!string.IsNullOrEmpty(pageToken));

                if (potentialEvents.Count == 0)
                    return Result<List<SearchEventRespone>>.FailureResult("No events found in the specified time range.");

                if (string.IsNullOrWhiteSpace(calendar.Title))
                {
                    return Result<List<SearchEventRespone>>.SuccessResult(potentialEvents);
                }

                var simplifiedList = potentialEvents.Select(e => new
                {
                    Id = e.Id,
                    Summary = e.Title,
                    Time = e.StartTime.ToString("HH:mm")
                }).ToList();

                string eventsJson = JsonSerializer.Serialize(simplifiedList);

                string filterPrompt = $@"
                Tôi đang tìm kiếm sự kiện trong lịch với ý định: ""{calendar.Title}"".
                Dưới đây là danh sách các sự kiện hiện có (JSON):
                {eventsJson}

                Nhiệm vụ: Hãy chọn ra các sự kiện có ý nghĩa phù hợp nhất với ý định tìm kiếm trên.
                - Chấp nhận tìm kiếm gần đúng, đồng nghĩa (ví dụ: tìm 'họp' thì chấp nhận 'meeting', 'sync').
                - Nếu tìm 'ăn trưa' thì chấp nhận 'lunch', 'tiệc'.
            
                OUTPUT: Chỉ trả về một mảng JSON chứa các chuỗi ID của sự kiện phù hợp. 
                Ví dụ: [""id_1"", ""id_2""]
                Nếu không có sự kiện nào khớp, trả về [].
                Tuyệt đối không giải thích thêm, không dùng markdown block.";


                await Task.Delay(500);
                var filterResponse = await _geminiClient.CallGemini(filterPrompt);
                var filterJson = filterResponse?.ToString()?.Replace("```json", "").Replace("```", "").Trim();

                List<string> matchedIds = new List<string>();
                try
                {
                    if (!string.IsNullOrWhiteSpace(filterJson))
                    {
                        if (filterJson.StartsWith("["))
                            matchedIds = JsonSerializer.Deserialize<List<string>>(filterJson);
                    }
                }
                catch
                {
                    
                }

                var finalResult = potentialEvents.Where(e => matchedIds.Contains(e.Id)).ToList();

                if (finalResult.Count == 0)
                {
                    var title = calendar.Title?.Trim();
                    if (!string.IsNullOrEmpty(title))
                    {
                        var simple = potentialEvents.Where(e => !string.IsNullOrEmpty(e.Title) &&
                                                                e.Title!.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                                                     .ToList();
                        if (simple.Count > 0)
                            finalResult = simple;
                    }
                }

                if (finalResult.Count == 0)
                    return Result<List<SearchEventRespone>>.FailureResult($"Found events on this day but none matched '{calendar.Title}'.");

                return Result<List<SearchEventRespone>>.SuccessResult(finalResult);
            }
            catch (Exception ex)
            {
                return Result<List<SearchEventRespone>>.FailureResult($"Error: {ex.Message}");
            }
        }

        public Task<Result<UpdateEventRespone>> UpdateEvent(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
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
    }
}
