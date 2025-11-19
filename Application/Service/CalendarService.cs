using Application.Abstractions.Infrastructure;
using Application.Abstractions.Services;
using Application.Contracts.Contact;
using Application.Contracts.ThirdParty.Calendar.Request;
using Application.Contracts.ThirdParty.Calendar.Respone;
using Application.Model;
using Shared.Common;
using System;
using System.Net;
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

        public async Task<Result<CreateEventRespone>> CreateEvent(MberModelRespone modelRespone, Guid userId)
        {
            string accessToken = await _oauthTokenService.GetAccessToken(userId);

            if (string.IsNullOrEmpty(accessToken))
            {
                return Result<CreateEventRespone>.FailureResult("Không tìm thấy access token.");
            }

            string prompt = $@"hãy phân tích câu ""{modelRespone.InputText}"" theo mẫu json sau:
                            {{
                                ""Title"": null,
                                ""StartTime"": null,
                                ""EndTime"": null,
                                ""Date"": null
                            }}
                            nếu thiếu trường nào đó thì trả về dạng json như trên và để null giá trị đó, giá trị Date phải ghi rõ ngày đó là ngày nào với ngày hôm nay là {DateTime.Now:dd/MM/yyyy} và không giải thích gì thêm";

            var llmResponse = await _geminiClient.CallGemini(prompt);

            var jsonString = llmResponse?.ToString()?.Replace("```json", "").Replace("```", "").Trim();
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                return Result<CreateEventRespone>.FailureResult("Không thể phân tích thông tin sự kiện từ câu nhập vào.");
            }

            CreateEventRequest? calendar;
            try
            {
                calendar = JsonSerializer.Deserialize<CreateEventRequest>(jsonString);
            }
            catch
            {
                return Result<CreateEventRespone>.FailureResult("Không thể phân tích thông tin sự kiện từ câu nhập vào.");
            }

            if (calendar == null || string.IsNullOrWhiteSpace(calendar.Title) || string.IsNullOrWhiteSpace(calendar.Date))
            {
                return Result<CreateEventRespone>.FailureResult("Thông tin sự kiện không đầy đủ.");
            }

            var newEvent = new
            {
                summary = calendar.Title,
                start = new { dateTime = $"{calendar.Date}T{calendar.StartTime}", timeZone = "Asia/Ho_Chi_Minh" },
                end = new { dateTime = $"{calendar.Date}T{calendar.EndTime}", timeZone = "Asia/Ho_Chi_Minh" }
            };

            var requestUri = "https://www.googleapis.com/calendar/v3/calendars/primary/events";
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(newEvent), System.Text.Encoding.UTF8, "application/json")
            };
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                var response = await _httpClient.SendAsync(requestMessage);
                return response.IsSuccessStatusCode
                    ? Result<CreateEventRespone>.SuccessResult(new CreateEventRespone { IsCreated = true }, "Sự kiện đã được tạo thành công.", HttpStatusCode.Created)
                    : Result<CreateEventRespone>.FailureResult("Tạo sự kiện thất bại.", statusCode: response.StatusCode);
            }
            catch (Exception ex)
            {
                return Result<CreateEventRespone>.FailureResult($"Đã xảy ra lỗi khi tạo sự kiện: {ex.Message}");
            }
        }

        public Task<Result<DeleteEventRespone>> DeleteEvent(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<SearchEventRespone>> SearchEvent(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<UpdateEventRespone>> UpdateEvent(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }
    }
}
