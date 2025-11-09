using Application.Abstractions.SignalR;
using BE.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace BE.SignalR
{
    public class NotificationHubContext : INotificationHubContext
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationHubContext(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task SendNotificationAsync(string message)
            => _hubContext.Clients.All.SendAsync("notification", message);

        public Task SendToClientAsync(string connectionId, string message)
            => _hubContext.Clients.Client(connectionId).SendAsync("notification", message);

        public Task SendToGroupAsync(string groupName, string message)
            => _hubContext.Clients.Group(groupName).SendAsync("notification", message);

        public Task SendToUserAsync(string userId, string message)
            => _hubContext.Clients.User(userId).SendAsync("notification", message);
    }
}
