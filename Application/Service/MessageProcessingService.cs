using Application.Abstractions.Services;
using System.Text.Json;

namespace Application.Service
{
    public class MessageProcessingService : IMessageProcessingService
    {
        public async Task<string> ProcessAsync(string raw)
        {
            await Task.Delay(0);

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
