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
                return Result<CreateEventRespone>.FailureResult(previewRes.Message ?? "Không thể tạo preview sự kiện");

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
                return Result<CreateEventRespone>.FailureResult("Payload thực thi không hợp lệ");

            return await ExecuteCreateAsync(execPayloadObj, userId);
        }

        public Task<Result<DeleteEventRespone>> DeleteEvent(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<SearchEventRespone>> SearchEvent(MberModelRespone modelRespone, Guid userId)
        {
            string accessToken = await _oauthTokenService.GetAccessToken(userId);
            if (string.IsNullOrEmpty(accessToken))
            {
                return Result<SearchEventRespone>.FailureResult("Không tìm thấy access token.");
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
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                return Result<SearchEventRespone>.FailureResult("Không thể phân tích thông tin sự kiện từ câu nhập vào.");
            }

            CreateEventRequest? calendar;
            try
            {
                calendar = JsonSerializer.Deserialize<CreateEventRequest>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return Result<SearchEventRespone>.FailureResult("Không thể phân tích thông tin sự kiện từ câu nhập vào.");
            }
            if (calendar == null)
            {
                return Result<SearchEventRespone>.FailureResult("Thông tin sự kiện không đầy đủ.");
            }

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
                request.MaxResults = 50;
                request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

                DateTime? start = null;
                DateTime? end = null;
                if (!string.IsNullOrWhiteSpace(calendar.StartDateTime))
                {
                    if (DateTime.TryParse(calendar.StartDateTime, out var stParsed))
                        start = stParsed;
                }
                if (!string.IsNullOrWhiteSpace(calendar.EndDateTime))
                {
                    if (DateTime.TryParse(calendar.EndDateTime, out var edParsed))
                        end = edParsed;
                }

                if (start.HasValue && !end.HasValue)
                {
                    end = start.Value.Date.AddDays(1).AddTicks(-1);
                }

                if (!start.HasValue && end.HasValue)
                {
                    start = end.Value.Date;
                }

                if (start.HasValue)
                {
                    request.TimeMinDateTimeOffset = new DateTimeOffset(start.Value.ToUniversalTime());
                }
                else
                {
                    request.TimeMinDateTimeOffset = new DateTimeOffset(DateTime.UtcNow);
                }

                if (end.HasValue)
                {
                    request.TimeMaxDateTimeOffset = new DateTimeOffset(end.Value.ToUniversalTime());
                }

                Events events = await request.ExecuteAsync();

                var eventList = new List<SearchEventRespone>();
                if (events.Items != null && events.Items.Count > 0)
                {
                    foreach (var eventItem in events.Items)
                    {
                        DateTime startTime;
                        if (!string.IsNullOrEmpty(eventItem.Start?.Date))
                        {
                            if (DateTime.TryParse(eventItem.Start.Date, out var d))
                            {
                                startTime = d.Date;
                            }
                            else
                            {
                                startTime = DateTime.MinValue;
                            }
                        }
                        else if (eventItem.Start?.DateTimeDateTimeOffset != null)
                        {
                            startTime = eventItem.Start.DateTimeDateTimeOffset.Value.UtcDateTime;
                        }
                        else
                        {
                            startTime = DateTime.MinValue;
                        }

                        DateTime endTime;
                        if (!string.IsNullOrEmpty(eventItem.End?.Date))
                        {
                            if (DateTime.TryParse(eventItem.End.Date, out var d2))
                            {
                                endTime = d2.Date;
                            }
                            else
                            {
                                endTime = DateTime.MinValue;
                            }
                        }
                        else if (eventItem.End?.DateTimeDateTimeOffset != null)
                        {
                            endTime = eventItem.End.DateTimeDateTimeOffset.Value.UtcDateTime;
                        }
                        else
                        {
                            endTime = DateTime.MinValue;
                        }

                        bool inRange = true;
                        if (start.HasValue && end.HasValue)
                        {
                            inRange = startTime != DateTime.MinValue && startTime >= start.Value && startTime <= end.Value;
                        }
                        else if (start.HasValue)
                        {
                            inRange = startTime != DateTime.MinValue && startTime.Date == start.Value.Date;
                        }

                        if (!inRange) continue;

                        if (!string.IsNullOrWhiteSpace(calendar.Title))
                        {
                            var titleLower = calendar.Title.Trim().ToLowerInvariant();
                            var evTitle = (eventItem.Summary ?? "").ToLowerInvariant();
                            if (!evTitle.Contains(titleLower))
                                continue;
                        }

                        eventList.Add(new SearchEventRespone
                        {
                            Id = eventItem.Id,
                            Title = eventItem.Summary,
                            StartTime = startTime,
                            EndTime = endTime
                        });
                    }
                }

                var found = eventList.FirstOrDefault();
                if (found == null)
                    return Result<SearchEventRespone>.FailureResult("Không tìm thấy sự kiện phù hợp.");
                return Result<SearchEventRespone>.SuccessResult(found);
            }
            catch (Exception ex)
            {
                return Result<SearchEventRespone>.FailureResult($"Lỗi khi tìm kiếm sự kiện Google Calendar: {ex.Message}");
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
                return Result<CalendarOperationPreview>.FailureResult("Không thể phân tích thông tin sự kiện từ câu nhập vào.");
            }

            CreateEventRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<CreateEventRequest>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return Result<CalendarOperationPreview>.FailureResult("Không thể phân tích thông tin sự kiện từ câu nhập vào.");
            }

            if (req == null)
                return Result<CalendarOperationPreview>.FailureResult("Thông tin sự kiện không đầy đủ.");

            var warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(req.Title)) warnings.Add("Thiếu tiêu đề sự kiện");
            if (string.IsNullOrWhiteSpace(req.StartDateTime) && string.IsNullOrWhiteSpace(req.EndDateTime)) warnings.Add("Thiếu thời gian bắt đầu/kết thúc");

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
                    else warnings.Add($"Không thể parse StartDateTime: '{req.StartDateTime}'");
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
                    else warnings.Add($"Không thể parse EndDateTime: '{req.EndDateTime}'");
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
                return Result<CreateEventRespone>.FailureResult("Không tìm thấy access token.");

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

                return Result<CreateEventRespone>.FailureResult("Không thể tạo sự kiện trên Google Calendar.");
            }
            catch (Exception ex)
            {
                return Result<CreateEventRespone>.FailureResult($"Lỗi khi tạo sự kiện Google Calendar: {ex.Message}");
            }
        }
    }
}
