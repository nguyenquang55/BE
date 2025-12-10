using Application.Abstractions.Infrastructure;
using Application.Abstractions.Repositories;
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
        private readonly IOAuthProviderRepository _oAuthProviderRepository;
        private readonly HttpClient _httpClient;

        public CalendarService(IRedisCacheService redisCacheService, IGeminiClient geminiClient, HttpClient httpClient, IOAuthTokenService oauthTokenService, IOAuthProviderRepository oAuthProviderRepository)
        {
            _oAuthProviderRepository = oAuthProviderRepository;
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
                return ExecuteDeleteAsync(new List<DeleteEventExecutionPayload> { execPayloadObj }, userId);
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
      
                      Reply naturally in English.
                      - If events exist, concisely list them (Time + Title).
                      - If the list is empty, state clearly and concisely that no events were found.
                      - Do not fabricate information.";

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
                return ExecuteUpdateAsync(new List<UpdateEventExecutionPayload> {execPayloadObj},userId);
            }
            return Task.FromResult(Result<UpdateEventRespone>.FailureResult("No events to update."));
        }

        public async Task<Result<List<CalendarOperationPreview>>> BuildUpdatePreviewAsync(MberModelRespone modelRespone, Guid userId)
        {
            var candidatesRes = await SearchEventsHelper(modelRespone, userId);

            if (!candidatesRes.Success || candidatesRes.Data == null || candidatesRes.Data.Count == 0)
            {
                return Result<List<CalendarOperationPreview>>.FailureResult("Can not find matching events to update.");
            }

            var candidates = candidatesRes.Data;

            var candidatesContext = candidates.Select(c => new
            {
                c.Id,
                CurrentTitle = c.Title,
                CurrentStart = c.StartTime.ToString("dd/MM/yyyy HH:mm"),
                CurrentEnd = c.EndTime.ToString("dd/MM/yyyy HH:mm")
            }).ToList();

            string candidatesJson = JsonSerializer.Serialize(candidatesContext);

            TimeZoneInfo userTimeZone;
            try { userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            catch { userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
            DateTime userNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone);

            string prompt = $@"
                Context: Today is {userNow:dd/MM/yyyy (dddd)} {userNow:HH:mm}.
                User Input: ""{modelRespone.InputText}""
    
                Candidate Events (JSON):
                {candidatesJson}

                Task:
                1. Identify which event(s) the user wants to update from the Candidate list.
                2. Extract the NEW values (NewTitle, NewStart, NewEnd).
                3. If user says 'delay 1 hour', calculate NewStart based on CurrentStart.
                4. If user only changes time, keep NewTitle null. If user only renames, keep dates null.

                Output Format (JSON Array only):
                [
                  {{
                    ""TargetEventId"": ""id_from_candidates"",
                    ""ExecutionPayload"": {{
                       ""NewTitle"": ""string or null"",
                       ""NewStart"": ""dd/MM/yyyy HH:mm or null"",
                       ""NewEnd"": ""dd/MM/yyyy HH:mm or null""
                    }}
                  }}
                ]
                ";

            var llmResponse = await _geminiClient.CallGemini(prompt);
            string jsonRes = CleanJson(llmResponse?.ToString() ?? "");

            try
            {
                var llmUpdates = JsonSerializer.Deserialize<List<UpdatePreviewDto>>(jsonRes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (llmUpdates == null || !llmUpdates.Any())
                    return Result<List<CalendarOperationPreview>>.FailureResult("AI could not determine update details.");

                var resultPreviews = new List<CalendarOperationPreview>();

                foreach (var update in llmUpdates)
                {
                    var originalEvent = candidates.FirstOrDefault(c => c.Id == update.TargetEventId);
                    if (originalEvent == null) continue;

                    var payload = update.ExecutionPayload;

                    DateTime? newStart = ParseUserDate(payload?.NewStart, userNow);
                    DateTime? newEnd = ParseUserDate(payload?.NewEnd, userNow);

                    if (newStart.HasValue && !newEnd.HasValue)
                    {
                        TimeSpan oldDuration = originalEvent.EndTime - originalEvent.StartTime;
                        newEnd = newStart.Value.Add(oldDuration);
                    }

                    var finalPayload = new UpdateEventExecutionPayload
                    {
                        EventId = update.TargetEventId,
                        NewTitle = payload?.NewTitle,
                        NewStart = newStart,
                        NewEnd = newEnd
                    };

                    // Build warnings for conflicts based on new time (if provided), else keep original time
                    var checkStart = finalPayload.NewStart ?? originalEvent.StartTime;
                    var checkEnd = finalPayload.NewEnd ?? originalEvent.EndTime;
                    var warnings = await BuildConflictWarnings(userId, checkStart, checkEnd, update.TargetEventId,modelRespone.InputText);

                    resultPreviews.Add(new CalendarOperationPreview
                    {
                        Action = "update",
                        TargetEventId = update.TargetEventId,
                        Title = originalEvent.Title,
                        Start = originalEvent.StartTime,
                        End = originalEvent.EndTime,
                        ExecutionPayload = finalPayload,
                        Warnings = warnings
                    });
                }

                if (resultPreviews.Count == 0)
                    return Result<List<CalendarOperationPreview>>.FailureResult("No valid updates parsed.");

                return Result<List<CalendarOperationPreview>>.SuccessResult(resultPreviews);
            }
            catch (Exception ex)
            {
                return Result<List<CalendarOperationPreview>>.FailureResult($"Error parsing update intent: {ex.Message}");
            }
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

        public async Task<Result<UpdateEventRespone>> ExecuteUpdateAsync(List<UpdateEventExecutionPayload> payloads, Guid userId)
        {
            if (payloads == null || !payloads.Any())
                return Result<UpdateEventRespone>.FailureResult("No events to update.");

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

                var updateTasks = payloads.Select(async item =>
                {
                    try
                    {
                        var getReq = googleService.Events.Get("primary", item.EventId);
                        var existing = await getReq.ExecuteAsync();

                        if (existing == null) return false;

                        // Capture old start/end before changes (UTC offsets)
                        DateTimeOffset? oldStartOffset = existing.Start?.DateTimeDateTimeOffset;
                        if (!oldStartOffset.HasValue && existing.Start != null && !string.IsNullOrEmpty(existing.Start.Date))
                        {
                            oldStartOffset = DateTimeOffset.Parse(existing.Start.Date);
                        }
                        DateTimeOffset? oldEndOffset = existing.End?.DateTimeDateTimeOffset;
                        if (!oldEndOffset.HasValue && existing.End != null && !string.IsNullOrEmpty(existing.End.Date))
                        {
                            oldEndOffset = DateTimeOffset.Parse(existing.End.Date);
                        }

                        bool isChanged = false;

                        if (!string.IsNullOrWhiteSpace(item.NewTitle))
                        {
                            existing.Summary = item.NewTitle;
                            isChanged = true;
                        }

                        if (item.NewStart.HasValue)
                        {
                            existing.Start = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(item.NewStart.Value.ToUniversalTime()) };
                            isChanged = true;
                        }

                        if (item.NewEnd.HasValue)
                        {
                            existing.End = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(item.NewEnd.Value.ToUniversalTime()) };
                            isChanged = true;
                        }

                        if (isChanged)
                        {
                            var updateReq = googleService.Events.Update(existing, "primary", item.EventId);
                            await updateReq.ExecuteAsync();
                        }

                        // Update Redis cache for 7-day tracking
                        TimeZoneInfo userTimeZone;
                        try { userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
                        catch { userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }

                        // Determine new start/end from existing (post-update values)
                        DateTimeOffset? newStartOffset = existing.Start?.DateTimeDateTimeOffset;
                        if (!newStartOffset.HasValue && existing.Start != null && !string.IsNullOrEmpty(existing.Start.Date))
                        {
                            newStartOffset = DateTimeOffset.Parse(existing.Start.Date);
                        }
                        DateTimeOffset? newEndOffset = existing.End?.DateTimeDateTimeOffset;
                        if (!newEndOffset.HasValue && existing.End != null && !string.IsNullOrEmpty(existing.End.Date))
                        {
                            newEndOffset = DateTimeOffset.Parse(existing.End.Date);
                        }

                        if (newStartOffset.HasValue && newEndOffset.HasValue)
                        {
                            var oldStartLocal = oldStartOffset.HasValue ? TimeZoneInfo.ConvertTime(oldStartOffset.Value, userTimeZone).DateTime : DateTime.MinValue;
                            var newStartLocal = TimeZoneInfo.ConvertTime(newStartOffset.Value, userTimeZone).DateTime;
                            var newEndLocal = TimeZoneInfo.ConvertTime(newEndOffset.Value, userTimeZone).DateTime;

                            if (oldStartLocal != DateTime.MinValue && oldStartLocal.Date != newStartLocal.Date)
                            {
                                await RemoveEventFromCache(userId, item.EventId, oldStartLocal);
                            }

                            var ev = new SearchEventRespone
                            {
                                Id = item.EventId,
                                Title = existing.Summary ?? "(No Title)",
                                StartTime = newStartLocal,
                                EndTime = newEndLocal
                            };
                            await UpsertEventInCache(userId, ev);
                        }

                        return true; 
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                });
                var results = await Task.WhenAll(updateTasks);

                int successCount = results.Count(r => r == true);
                int totalCount = payloads.Count;

                if (successCount == 0)
                {
                    return Result<UpdateEventRespone>.FailureResult("Failed to update any events.");
                }

                return Result<UpdateEventRespone>.SuccessResult(new UpdateEventRespone
                {
                    IsUpdated = true,
                });
            }
            catch (Exception ex)
            {
                return Result<UpdateEventRespone>.FailureResult($"System Error: {ex.Message}");
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
                        Event? existing = null;
                        try
                        {
                            var getReq = googleService.Events.Get("primary", payloadid.EventId);
                            existing = await getReq.ExecuteAsync();
                        }
                        catch { }

                        var delReq = googleService.Events.Delete("primary", payloadid.EventId);
                        await delReq.ExecuteAsync();

                        if (existing != null)
                        {
                            TimeZoneInfo userTimeZone;
                            try { userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
                            catch { userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }

                            DateTimeOffset? oldStartOffset = existing.Start?.DateTimeDateTimeOffset;
                            if (!oldStartOffset.HasValue && existing.Start != null && !string.IsNullOrEmpty(existing.Start.Date))
                            {
                                oldStartOffset = DateTimeOffset.Parse(existing.Start.Date);
                            }
                            if (oldStartOffset.HasValue)
                            {
                                var oldStartLocal = TimeZoneInfo.ConvertTime(oldStartOffset.Value, userTimeZone).DateTime;
                                await RemoveEventFromCache(userId, payloadid.EventId, oldStartLocal);
                            }
                        }
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

            // Add conflict warnings if we have full time range
            if (hasStart && hasEnd)
            {
                var conflictWarnings = await BuildConflictWarnings(userId, startDt, endDt, null,modelRespone.InputText);
                if (conflictWarnings.Count > 0)
                    warnings.AddRange(conflictWarnings);
            }

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
                {
                    TimeZoneInfo userTimeZone;
                    try { userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
                    catch { userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }

                    var startLocal = TimeZoneInfo.ConvertTime(new DateTimeOffset(startDateTime), userTimeZone).DateTime;
                    var endLocal = TimeZoneInfo.ConvertTime(new DateTimeOffset(endDateTime), userTimeZone).DateTime;

                    var ev = new SearchEventRespone
                    {
                        Id = createdEvent.Id,
                        Title = payload.Title,
                        StartTime = startLocal,
                        EndTime = endLocal
                    };
                    await UpsertEventInCache(userId, ev);

                    return Result<CreateEventRespone>.SuccessResult(new CreateEventRespone { IsCreated = true });
                }

                return Result<CreateEventRespone>.FailureResult("Unable to create event on Google Calendar.");
            }
            catch (Exception ex)
            {
                return Result<CreateEventRespone>.FailureResult($"Error creating Google Calendar event: {ex.Message}");
            }
        }
        #endregion

        #region Helpers
        private async Task<string> DayKey(Guid userId, DateTime day)
        {
            string? primaryGoogleEmail = null;
            try
            {
                primaryGoogleEmail = await _redisCacheService.GetAsync<string>($"primaryGoogleAccount:{userId}");

                if (string.IsNullOrEmpty(primaryGoogleEmail))
                {

                    var acct = await _oAuthProviderRepository.GetPrimaryGoogleAccountAsync(userId);
                    if (acct != null)
                    {
                        primaryGoogleEmail = acct.ProviderEmail;
                        if (!string.IsNullOrWhiteSpace(primaryGoogleEmail))
                        {
                            await _redisCacheService.SetAsync($"primaryGoogleAccount:{userId}", primaryGoogleEmail, TimeSpan.FromDays(8));
                        }
                    }
                }
            }
            catch
            {
                primaryGoogleEmail = null;
            }

            var d = day.Date;
            var yyyyMMdd = d.ToString("yyyyMMdd");
            var acctPart = string.IsNullOrWhiteSpace(primaryGoogleEmail) ? "default" : primaryGoogleEmail;
            return $"user:{userId}:acct:{acctPart}:events:{yyyyMMdd}";
        }

        private async Task<List<SearchEventRespone>?> GetDayEventsFromCache(Guid userId, DateTime day)
        {
            var key = await DayKey(userId, day);
            return await _redisCacheService.GetAsync<List<SearchEventRespone>>(key);
        }

        private async Task SetDayEventsCache(Guid userId, DateTime day, List<SearchEventRespone> events)
        {
            var key = await DayKey(userId, day);
            var ttl = TimeSpan.FromDays(8); 
            await _redisCacheService.SetAsync(key, events, ttl);
        }

        private async Task<List<SearchEventRespone>?> TryGetEventsFromCache(Guid userId, DateTime startUserTime, DateTime endUserTime)
        {
            var today = DateTime.Now.Date;
            var maxDay = today.AddDays(7);
            if (startUserTime.Date < today || endUserTime.Date > maxDay)
                return null;

            var list = new List<SearchEventRespone>();
            for (var d = startUserTime.Date; d <= endUserTime.Date; d = d.AddDays(1))
            {
                var dayEvents = await GetDayEventsFromCache(userId, d);
                if (dayEvents == null)
                    return null;
                list.AddRange(dayEvents);
            }
            list = list.Where(e => e.StartTime < endUserTime && e.EndTime > startUserTime).ToList();
            return list;
        }

        private async Task SaveEventsToCache(Guid userId, IEnumerable<SearchEventRespone> events, DateTime userNow)
        {
            var startWindow = userNow.Date;
            var endWindow = startWindow.AddDays(7);
            var groups = events
                .Where(e => e.StartTime.Date >= startWindow && e.StartTime.Date <= endWindow)
                .GroupBy(e => e.StartTime.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kv in groups)
            {
                await SetDayEventsCache(userId, kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// Warm up Redis cache with all events in the next 7 days for the user's active Google account.
        /// Skips adding duplicates that already exist in Redis.
        /// Intended to be called on connection/opening a session.
        /// </summary>
        public async Task<Result<int>> WarmupCacheNext7DaysForActiveAccount(Guid userId)
        {
            TimeZoneInfo userTimeZone;
            try { userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            catch { userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }

            var userNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone);
            var startUserTime = userNow.Date;
            var endUserTime = startUserTime.AddDays(7).AddTicks(-1);

            // Get access token for current active (primary) account
            var accessToken = await _oauthTokenService.GetAccessToken(userId);
            if (string.IsNullOrEmpty(accessToken))
            {
                return Result<int>.FailureResult("Access token not found for active account.");
            }

            // Prepare Google service client
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startUserTime, userTimeZone);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(endUserTime, userTimeZone);

            var googleService = new Google.Apis.Calendar.v3.CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken(accessToken),
                ApplicationName = "CalendarApp"
            });

            var collected = new List<SearchEventRespone>();
            string? pageToken = null;

            try
            {
                do
                {
                    var request = googleService.Events.List("primary");
                    request.ShowDeleted = false;
                    request.SingleEvents = true;
                    request.MaxResults = 100;
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
                            var itemStartOffset = item.Start.DateTimeDateTimeOffset ?? DateTimeOffset.Parse(item.Start.Date);
                            var itemEndOffset = item.End.DateTimeDateTimeOffset ?? DateTimeOffset.Parse(item.End.Date);

                            var itemStartUser = TimeZoneInfo.ConvertTime(itemStartOffset, userTimeZone).DateTime;
                            var itemEndUser = TimeZoneInfo.ConvertTime(itemEndOffset, userTimeZone).DateTime;

                            collected.Add(new SearchEventRespone
                            {
                                Id = item.Id,
                                Title = item.Summary ?? "(No Title)",
                                StartTime = itemStartUser,
                                EndTime = itemEndUser
                            });
                        }
                    }

                    pageToken = events.NextPageToken;
                }
                while (!string.IsNullOrEmpty(pageToken));
            }
            catch (Exception ex)
            {
                return Result<int>.FailureResult($"Google API Error during warmup: {ex.Message}");
            }

            var startDay = startUserTime.Date;
            var endDay = endUserTime.Date;
            int addedCount = 0;

            // Group by day
            var groups = collected.GroupBy(e => e.StartTime.Date).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kv in groups)
            {
                var day = kv.Key;
                if (day < startDay || day > endDay) continue;

                var existing = await GetDayEventsFromCache(userId, day) ?? new List<SearchEventRespone>();
                var existingIds = existing.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);

                var toAdd = kv.Value.Where(x => !existingIds.Contains(x.Id)).ToList();
                if (toAdd.Count == 0) continue;

                existing.AddRange(toAdd);
                await SetDayEventsCache(userId, day, existing);
                addedCount += toAdd.Count;
            }

            return Result<int>.SuccessResult(addedCount);
        }

        private async Task UpsertEventInCache(Guid userId, SearchEventRespone ev)
        {
            var day = ev.StartTime.Date;
            var list = await GetDayEventsFromCache(userId, day) ?? new List<SearchEventRespone>();
            list = list.Where(x => x.Id != ev.Id).ToList();
            list.Add(ev);
            await SetDayEventsCache(userId, day, list);
        }

        private async Task RemoveEventFromCache(Guid userId, string eventId, DateTime oldStart)
        {
            var day = oldStart.Date;
            var list = await GetDayEventsFromCache(userId, day);
            if (list == null) return;
            list = list.Where(x => x.Id != eventId).ToList();
            await SetDayEventsCache(userId, day, list);
        }

        private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
            => aStart < bEnd && aEnd > bStart;

        private async Task<List<string>> BuildConflictWarnings(Guid userId, DateTime newStart, DateTime newEnd, string? excludeEventId, string modelMessage)
        {
            var warnings = new List<string>();
            var message = modelMessage;
            bool isVi = IsVietnamese(message);

            var conflicts = new List<SearchEventRespone>();
            for (var d = newStart.Date; d <= newEnd.Date; d = d.AddDays(1))
            {
                var dayEvents = await GetDayEventsFromCache(userId, d);
                if (dayEvents == null) continue;
                foreach (var ev in dayEvents)
                {
                    if (!string.IsNullOrEmpty(excludeEventId) && ev.Id == excludeEventId) continue;
                    if (Overlaps(newStart, newEnd, ev.StartTime, ev.EndTime)) conflicts.Add(ev);
                }
            }

            if (conflicts.Count == 0)
                return warnings;
            try
            {
                var candidates = conflicts.Select(c => new
                {
                    c.Id,
                    c.Title,
                    Start = c.StartTime.ToString("dd/MM/yyyy HH:mm"),
                    End = c.EndTime.ToString("dd/MM/yyyy HH:mm")
                }).ToList();
                var candidatesJson = JsonSerializer.Serialize(candidates);

                var prompt = isVi ? $@"
                Nhiệm vụ: Viết cảnh báo xung đột lịch bằng tiếng Việt thật ngắn gọn, dễ hiểu.
                Thời gian đề xuất: {newStart:dd/MM/yyyy HH:mm} - {newEnd:dd/MM/yyyy HH:mm}
                Các sự kiện đang trùng (JSON):
                {candidatesJson}

                Yêu cầu:
                - Tóm tắt số lượng và liệt kê 2-3 sự kiện trùng (tiêu đề + khung giờ).
                - Đưa ra 2-3 gợi ý thời điểm thay thế hợp lý (có thể dịch chuyển 15-30 phút, hoặc cùng khung giờ ngày hôm sau).
                - Trả về dưới dạng các dòng văn bản, tối đa 5-6 dòng, không giải thích dài dòng, không trả về JSON.
                " : $@"
                Task: Write a concise, easy-to-understand calendar conflict warning in English.
                Proposed time: {newStart:dd/MM/yyyy HH:mm} - {newEnd:dd/MM/yyyy HH:mm}
                Conflicting events (JSON):
                {candidatesJson}

                Requirements:
                - Summarize the count and list 2-3 overlapping events (title + time range).
                - Provide 2-3 reasonable alternative time suggestions (e.g., shift 15-30 mins, or same time next day).
                - Return as plain text lines, max 5-6 lines, no verbose explanation, do not return JSON.
                ";

                var llm = await _geminiClient.CallGemini(prompt);
                var text = llm?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(text))
                {
                    foreach (var line in text.Split('\n'))
                    {
                        var clean = line.Trim();
                        if (string.IsNullOrEmpty(clean)) continue;
                        warnings.Add(clean);
                        if (warnings.Count >= 6) break;
                    }
                }
                else
                {
                    warnings.Add($"Phát hiện {conflicts.Count} xung đột thời gian với sự kiện hiện có.");
                }
            }
            catch
            {
                warnings.Add($"Phát hiện {conflicts.Count} xung đột thời gian với sự kiện hiện có.");
                foreach (var c in conflicts.Take(3))
                    warnings.Add($"- '{c.Title}' ({c.StartTime:dd/MM HH:mm} - {c.EndTime:dd/MM HH:mm})");
            }

            return warnings;
        }
        private async Task<Result<List<SearchEventRespone>>> SearchEventsHelper(MberModelRespone modelRespone, Guid userId)
        {
            string accessToken = await _oauthTokenService.GetAccessToken(userId);
            if (string.IsNullOrEmpty(accessToken))
                return Result<List<SearchEventRespone>>.FailureResult("Access token not found.");

            TimeZoneInfo userTimeZone;
            try { userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            catch { userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }

            DateTime userNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone);

            string intent = modelRespone.Intent?.ToLower() ?? "search";

            string extractionPrompt;

            if (intent.Contains("update"))
            {
                extractionPrompt = $@"
                Input: ""{modelRespone.InputText}""
                Context: Today is {userNow:dd/MM/yyyy (dddd)}.
                Task: Extract **EXISTING** event details for search.

                CRITICAL RULES FOR DATES:
                1. **ABSOLUTE DATES ONLY**: You MUST calculate ""tomorrow"", ""next week"", ""this afternoon"" into specific dates based on Context. NEVER return strings like ""tomorrow"".
                2. **FORMAT**: All dates MUST be ""dd/MM/yyyy HH:mm"".
                3. **MISSING DAY (TIME-ONLY resolution) - QUY TẮC MỚI**: Nếu chỉ một thời gian được chỉ định mà không có ngày (ví dụ: ""5pm""), ngày MẶC ĐỊNH PHẢI là **HÔM NAY** (sử dụng ngày trong Context), ngay cả khi thời gian đó đã trôi qua.

                CRITICAL RULES FOR UPDATE INTENT:
                4. **IGNORE THE NEW TIME**: Extract **CHỈ** các chi tiết của sự kiện **HIỆN TẠI/CŨ**. ""8pm"" là thời gian MỚI, phải bỏ qua. Thời gian HIỆN TẠI là ""5pm"".

                CRITICAL RULES FOR SEARCH RANGE:
                5. **WHOLE DAY SEARCH**: If user mentions a specific day but NO specific time (e.g., ""meeting tomorrow""), you must define the search range for the WHOLE day: StartDateTime = ""dd/MM/yyyy 00:00"", EndDateTime = ""dd/MM/yyyy 23:59"".
                6. **SPECIFIC START TIME TO END OF DAY (QUY TẮC MỚI)**: Nếu người dùng cung cấp một thời điểm **BẮT ĐẦU HIỆN TẠI** (như 5pm) nhưng không có thời điểm kết thúc cho sự kiện hiện tại, phạm vi tìm kiếm **PHẢI** được đặt từ thời điểm bắt đầu đó cho đến cuối ngày (23:59).

                Format:
                {{
                    ""Title"": string|null (original keywords),
                    ""StartDateTime"": string (dd/MM/yyyy HH:mm),
                    ""EndDateTime"": string (dd/MM/yyyy HH:mm)
                }}
                Return ONLY JSON.";
            }
            else
            {
                extractionPrompt = $@"
                Input: ""{modelRespone.InputText}""
                Context: Today is {userNow:dd/MM/yyyy (dddd)}.
                Task: Extract search criteria.

                CRITICAL RULES FOR DATES:
                1. **ABSOLUTE DATES ONLY**: You MUST calculate relative dates (""""tomorrow"""", """"next Monday"""") into specific dates based on Context. NEVER return strings like """"tomorrow"""".
                2. **FORMAT**: All dates MUST be """"dd/MM/yyyy HH:mm"""".

                CRITICAL RULES FOR TIME & RANGES:
                1. **SPECIFIC TIME POINT (Start/End)**: If the user specifies a specific time (e.g., """"05:00"""") **VÀ** một buổi trong ngày (""""sáng/chiều/tối""""):
                    - Set **StartDateTime** là thời điểm cụ thể được nhắc đến.
                    - Set **EndDateTime** là thời điểm kết thúc của buổi trong ngày đó.
                    - Ví dụ: """"lịch chạy bộ lúc 05:00 sáng mai"""" -> Start=""""... 05:00"""", End=""""... 11:59"""" (Kết thúc buổi sáng).
                2. **SPECIFIC TIME RANGE**: If user specifies a clear time range (e.g., """"from 2pm to 4pm""""), set **StartDateTime** và **EndDateTime** tới các mốc chính xác.
                3. **TIME-OF-DAY ONLY / JUST DAY / SPECIFIC TIME ONLY (NO RANGE)**: Nếu người dùng chỉ đề cập đến buổi trong ngày, chỉ ngày, hoặc chỉ một điểm thời gian đơn lẻ (không có buổi đi kèm hoặc không có phạm vi), phải tìm kiếm toàn bộ phạm vi.
                    - **""sáng"" (morning):** Start 00:00, End 11:59.
                    - **""chiều"" (afternoon):** Start 12:00, End 17:59.
                    - **""tối"" (evening):** Start 18:00, End 23:59.
                    - **Just Day (no time/no time-of-day):** Start 00:00, End 23:59.
                    - **Specific Time Only (e.g. """"15:00"""" without 'sáng/chiều/tối'):** Start 00:00, End 23:59.

                Format:
                {{{{
                    """"Title"""": string|null,
                    """"StartDateTime"""": string|null (dd/MM/yyyy HH:mm),
                    """"EndDateTime"""": string|null (dd/MM/yyyy HH:mm)
                }}}}
                Return ONLY JSON.";
            }

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

            if (calendarIntent == null) return Result<List<SearchEventRespone>>.FailureResult("Unable to understand search criteria.");

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

            var cached = await TryGetEventsFromCache(userId, startUserTime, endUserTime);
            List<SearchEventRespone> allEvents;
            if (cached != null)
            {
                allEvents = cached;
            }
            else
            {
                var googleService = new Google.Apis.Calendar.v3.CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken(accessToken),
                    ApplicationName = "CalendarApp"
                });

                allEvents = new List<SearchEventRespone>();
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

                await SaveEventsToCache(userId, allEvents, userNow);
            }

            var filteredEvents = allEvents;
            string intentTitle = calendarIntent.Title?.Trim();

            if (!string.IsNullOrEmpty(intentTitle))
            {
                if (allEvents.Count > 0)
                {
                    var candidates = allEvents.Select(e => new { e.Id, e.Title, Time = e.StartTime.ToString("dd/MM HH:mm") }).ToList();
                    string candidatesJson = JsonSerializer.Serialize(candidates);

                    string filterPrompt = $@"
                    Task: Identify event IDs that match the description: ""{intentTitle}""
                    Candidates: {candidatesJson}
                    Return JSON array of IDs: [""id1"", ""id2""]. If none, return [].";

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
                            if (simpleFilter.Any()) filteredEvents = simpleFilter;
                        }
                    }
                    catch { /* Ignore LLM filter error */ }
                }
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

        private class UpdatePreviewDto
        {
            public string TargetEventId { get; set; } = string.Empty;
            public UpdatePayloadDto ExecutionPayload { get; set; } = new();
        }

        private class UpdatePayloadDto
        {
            public string? NewTitle { get; set; }
            public string? NewStart { get; set; }
            public string? NewEnd { get; set; }
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
