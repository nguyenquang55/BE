using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Application.Abstractions.Services;
using Application.Abstractions.SignalR;
using System.Text.Json;

namespace BE.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly IMessageEnqueueService _enqueueService;
        private readonly INotificationHubContext _notificationHubContext;

        public NotificationHub(IMessageEnqueueService enqueueService, INotificationHubContext notificationHubContext)
        {
            _enqueueService = enqueueService;
            _notificationHubContext = notificationHubContext;
        }
        public override async Task OnConnectedAsync()
        {

        }
        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        public async Task ProcessMessage(string message, string messageId)
        {
            var (mid, trace) = await _enqueueService.EnqueueAsync(
                payload: message,
                userId: Context.UserIdentifier,
                connectionId: Context.ConnectionId,
                messageId: string.IsNullOrWhiteSpace(messageId) ? null : messageId
            );

            var ackJson = JsonSerializer.Serialize(new
            {
                type = "ack",
                messageId = mid,
                traceId = trace,
                status = "in-progress"
            });

            if (!string.IsNullOrWhiteSpace(Context.UserIdentifier))
                await _notificationHubContext.SendToUserAsync(Context.UserIdentifier!, ackJson);
            else
                await _notificationHubContext.SendToClientAsync(Context.ConnectionId, ackJson);
        }
    }
}
