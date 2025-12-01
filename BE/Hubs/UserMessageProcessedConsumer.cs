using Application.Abstractions.Services;
using Application.Abstractions.SignalR;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Common;
using Shared.Contracts.Messaging;
using System.Text.Json;
using System.Threading.Tasks;

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
                type = RealtimeMessageTypes.Processed,
                ResultType = evt.ResultType,
                payload = evt.ProcessingResult,
                messageId = evt.MessageId,
                traceId = evt.TraceId,
                connectionId = route.ConnectionId
            });

            await _hubContext.SendToClientAsync(route.ConnectionId, payload);

            await _routingStore.RemoveAsync(evt.MessageId);
        }
    }
}
