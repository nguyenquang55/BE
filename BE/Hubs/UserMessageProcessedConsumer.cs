using System.Text.Json;
using System.Threading.Tasks;
using Application.Abstractions.SignalR;
using Application.Abstractions.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Messaging;

namespace BE.Hubs
{
    public class UserMessageProcessedConsumer : IConsumer<UserMessageProcessedIntegrationEvent>
    {
        private readonly INotificationHubContext _hubContext;
        private readonly IRoutingStore _routingStore;
        private readonly ILogger<UserMessageProcessedConsumer> _logger;

        public UserMessageProcessedConsumer(INotificationHubContext hubContext, IRoutingStore routingStore, ILogger<UserMessageProcessedConsumer> logger)
        {
            _hubContext = hubContext;
            _routingStore = routingStore;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<UserMessageProcessedIntegrationEvent> context)
        {
            var evt = context.Message;
            var route = await _routingStore.TryGetAsync(evt.MessageId);
            if (route == null)
            {
                _logger.LogWarning("Route missing for message {MessageId}", evt.MessageId);
                return;
            }

            var payload = JsonSerializer.Serialize(new
            {
                type = "processed",
                payload = evt.ProcessingResult,
                messageId = evt.MessageId,
                traceId = evt.TraceId,
                connectionId = route.ConnectionId
            });

            if (!string.IsNullOrWhiteSpace(route.UserId))
                await _hubContext.SendToUserAsync(route.UserId!, payload);
            else
                await _hubContext.SendToClientAsync(route.ConnectionId, payload);

            await _routingStore.RemoveAsync(evt.MessageId);
        }
    }
}
