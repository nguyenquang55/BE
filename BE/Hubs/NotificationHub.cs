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

        public NotificationHub(IMessageProcessingService messageProcessingService, INotificationHubContext notificationHubContext)
        {
            _messageProcessingService = messageProcessingService;
            _notificationHubContext = notificationHubContext;
        }
        public override async Task OnConnectedAsync()
        {

        }
        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        //public Task Echo(string message)
        //{
        //    var http = Context.GetHttpContext();
        //    Console.ForegroundColor = ConsoleColor.Green;
        //    Console.WriteLine($"[WS MESSAGE] ConnId={Context.ConnectionId} Message={message}");
        //    Console.ResetColor();
        //    return Clients.Caller.SendAsync("echo", message);
        //}

        public async Task ProcessMessage(string message, string messageId)
        {
            var processed = await _messageProcessingService.ProcessAsync(message);

            var processedJson = JsonSerializer.Serialize(new
            {
                type = "processed",
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
