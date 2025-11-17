using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface IMessageEnqueueService
    {
        Task<(string messageId, string traceId)> EnqueueAsync(string payload,string userId, string connectionId, string? messageId = null, string? traceId = null, CancellationToken ct = default);
    }
}
