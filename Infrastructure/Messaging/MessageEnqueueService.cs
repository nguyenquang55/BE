using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Messaging;

namespace Infrastructure.Messaging
{
    public class MessageEnqueueService : IMessageEnqueueService
    {
        private readonly IRoutingStore _routingStore;
        private readonly IPublishEndpoint _publisher;
        private readonly ILogger<MessageEnqueueService> _logger;

        public MessageEnqueueService(IRoutingStore routingStore, IPublishEndpoint publisher, ILogger<MessageEnqueueService> logger)
        {
            _routingStore = routingStore;
            _publisher = publisher;
            _logger = logger;
        }

        public async Task<(string messageId, string traceId)> EnqueueAsync(string payload, string? UserId, string connectionId, string? messageId = null, string? traceId = null, CancellationToken ct = default)
        {
            var mid = messageId ?? Guid.NewGuid().ToString("N");
            var tid = traceId ?? Guid.NewGuid().ToString("N");

            await _routingStore.SaveAsync(mid, new MessageRoute(UserId, connectionId), TimeSpan.FromMinutes(10), ct);

            var evt = new UserMessageSubmittedIntegrationEvent(
                MessageId: mid,
                userId: UserId,
                ConnectionId: connectionId,
                Payload: payload,
                TraceId: tid,
                CreatedAt: DateTimeOffset.UtcNow
            );

            await _publisher.Publish(evt, ct);
            _logger.LogInformation("Published submitted event {MessageId} trace {TraceId}", mid, tid);
            return (mid, tid);
        }
    }
}
