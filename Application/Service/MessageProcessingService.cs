using Application.Abstractions.Services;
using System.Text.Json;

namespace Application.Service
{
    /// <summary>
    /// Demo implement xử lý message: giả lập độ trễ và chuẩn hoá payload sang JSON.
    /// </summary>
    public class MessageProcessingService : IMessageProcessingService
    {
        public async Task<string> ProcessAsync(string raw)
        {
            await Task.Delay(500);

            var normalized = new
            {
                type = "processed",
                original = raw,
                length = raw?.Length ?? 0,
                upper = raw?.ToUpperInvariant(),
                processedAt = DateTimeOffset.UtcNow
            };
            return JsonSerializer.Serialize(normalized);
        }
    }
}
