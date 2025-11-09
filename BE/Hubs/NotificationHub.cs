using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Application.Abstractions.Services;
using Application.Abstractions.SignalR;
using System.Text.Json;

namespace BE.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly IMessageProcessingService _messageProcessingService;
        private readonly INotificationHubContext _notificationHubContext;

        public NotificationHub(IMessageProcessingService messageProcessingService,
                               INotificationHubContext notificationHubContext)
        {
            _messageProcessingService = messageProcessingService;
            _notificationHubContext = notificationHubContext;
        }
        public override async Task OnConnectedAsync()
        {
            var http = Context.GetHttpContext();
            if (!string.IsNullOrWhiteSpace(Context.UserIdentifier))
            {
                await _notificationHubContext.SendToUserAsync(Context.UserIdentifier!, $"Hello {Context.UserIdentifier}");
            }
            else
            {
                await _notificationHubContext.SendToClientAsync(Context.ConnectionId, $"Hello {Context.ConnectionId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        public Task Echo(string message)
        {
            var http = Context.GetHttpContext();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[WS MESSAGE] ConnId={Context.ConnectionId} Message={message}");
            Console.ResetColor();
            return Clients.Caller.SendAsync("echo", message);
        }

        /// <summary>
        /// Client gửi message lên để xử lý; server trả về 2 bước:
        /// 1. Ack ngay (type=ack)
        /// 2. Sau khi xử lý xong gửi lại (type=processed)
        /// </summary>
        public async Task ProcessMessage(string message, string messageId)
        {
            // Bước 1: gửi ack
            var ackJson = JsonSerializer.Serialize(new
            {
                type = "ack",
                messageId,
                receivedAt = DateTimeOffset.UtcNow,
                connectionId = Context.ConnectionId
            });
            if (!string.IsNullOrWhiteSpace(Context.UserIdentifier))
            {
                await _notificationHubContext.SendToUserAsync(Context.UserIdentifier!, ackJson);
            }
            else
            {
                await _notificationHubContext.SendToClientAsync(Context.ConnectionId, ackJson);
            }

            // Bước 2: xử lý bất đồng bộ
            var processed = await _messageProcessingService.ProcessAsync(message);

            // Bước 3: gửi kết quả
            var processedJson = JsonSerializer.Serialize(new
            {
                type = "processed",
                messageId,
                payload = processed,
                connectionId = Context.ConnectionId
            });
            if (!string.IsNullOrWhiteSpace(Context.UserIdentifier))
            {
                await _notificationHubContext.SendToUserAsync(Context.UserIdentifier!, processedJson);
            }
            else
            {
                await _notificationHubContext.SendToClientAsync(Context.ConnectionId, processedJson);
            }
        }
    }
}
