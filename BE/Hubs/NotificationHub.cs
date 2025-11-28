using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Application.Abstractions.Services;
using Application.Abstractions.SignalR;
using System.Text.Json;
using Shared.Common;
using MassTransit;
using Shared.Contracts.Messaging;
using Org.BouncyCastle.Asn1.Cms;

namespace BE.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly IMessageEnqueueService _enqueueService;
        private readonly INotificationHubContext _notificationHubContext;
        private readonly IRoutingStore _routingStore;
        private readonly IPublishEndpoint _publisher;
        public NotificationHub(IMessageEnqueueService enqueueService, INotificationHubContext notificationHubContext, IRoutingStore routingStore, IPublishEndpoint publisher)
        {
            _enqueueService = enqueueService;
            _notificationHubContext = notificationHubContext;
            _routingStore = routingStore;
            _publisher = publisher;
        }
        public override async Task OnConnectedAsync()
        {

        }
        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        public async Task ProcessMessage(string message, string messageId, Guid userId)
        {
            await Task.Delay(100); 

            var (mid, trace) = await _enqueueService.EnqueueAsync(
                payload: message,
                userId: userId.ToString(),
                connectionId: Context.ConnectionId,
                messageId: string.IsNullOrWhiteSpace(messageId) ? null : messageId
            );

            var ackJson = JsonSerializer.Serialize(new
            {
                type = RealtimeMessageTypes.Ack,
                messageId = mid,
                traceId = trace,
                status = "in-progress"
            });

            if (!string.IsNullOrWhiteSpace(Context.UserIdentifier))
                await _notificationHubContext.SendToUserAsync(Context.UserIdentifier!, ackJson);
            else
                await _notificationHubContext.SendToClientAsync(Context.ConnectionId, ackJson);
        }

        public async Task ConfirmOperation(string messageId, string resultType, bool confirmed, string? executionPayloadJson, string? traceId = null)
        {
            var payload = JsonSerializer.Serialize(new
            {
                type = RealtimeMessageTypes.DecisionAccepted,
                messageId,
                confirmed,
                executionPayload = executionPayloadJson
            });

            await _notificationHubContext.SendToClientAsync(Context.ConnectionId, payload);

            var route = await _routingStore.TryGetAsync(messageId);
            var userId = route?.UserId;

            object? execPayload = null;
            if (!string.IsNullOrWhiteSpace(executionPayloadJson))
            {
                try { execPayload = JsonSerializer.Deserialize<object>(executionPayloadJson!); }
                catch { execPayload = executionPayloadJson; }
            }

            var evt = new UserPreviewDecisionIntegrationEvent(
                MessageId: messageId,
                UserId: userId,
                ConnectionId: Context.ConnectionId,
                ResultType: resultType,
                Confirmed: confirmed,
                ExecutionPayload: execPayload,
                TraceId: string.IsNullOrWhiteSpace(traceId) ? System.Guid.NewGuid().ToString("N") : traceId!,
                DecidedAt: DateTime.Now
            );
            await _publisher.Publish(evt);
        }
    }
}
