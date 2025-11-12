using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public record MessageRoute(string? UserId, string ConnectionId);

    public interface IRoutingStore
    {
        Task SaveAsync(string messageId, MessageRoute route, TimeSpan ttl, CancellationToken ct = default);
        Task<MessageRoute?> TryGetAsync(string messageId, CancellationToken ct = default);
        Task RemoveAsync(string messageId, CancellationToken ct = default);
    }
}
