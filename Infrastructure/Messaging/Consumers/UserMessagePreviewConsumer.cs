using System.Text.Json;
using System.Threading.Tasks;
using Application.Abstractions.Services;
using Application.Abstractions.SignalR;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Common;
using Shared.Contracts.Messaging;

namespace Infrastructure.Messaging.Consumers
{
    public class UserMessagePreviewConsumer : IConsumer<UserMessagePreviewIntegrationEvent>
    {
        private readonly INotificationHubContext _hubContext;
        private readonly IRoutingStore _routingStore;
        private readonly ILogger<UserMessagePreviewConsumer> _logger;

        public UserMessagePreviewConsumer(INotificationHubContext hubContext, IRoutingStore routingStore, ILogger<UserMessagePreviewConsumer> logger)
        {
            _hubContext = hubContext;
            _routingStore = routingStore;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<UserMessagePreviewIntegrationEvent> context)
        {
            var evt = context.Message;
            var route = await _routingStore.TryGetAsync(evt.MessageId);
            if (route == null)
            {
                _logger.LogWarning("Route missing for preview message {MessageId}", evt.MessageId);
                return;
            }

            var payload = JsonSerializer.Serialize(new
            {
                type = RealtimeMessageTypes.Preview,
                messageId = evt.MessageId,
                resultType = evt.ResultType,
                expiresAt = evt.ExpiresAt,
                preview = evt.PreviewPayload,
                traceId = evt.TraceId,
                connectionId = route.ConnectionId
            });

            await _hubContext.SendToClientAsync(route.ConnectionId, payload);
        }
    }
}
