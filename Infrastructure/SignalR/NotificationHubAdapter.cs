using Application.Abstractions.SignalR;
using System.Threading.Tasks;

namespace Infrastructure.SignalR
{
    /// <summary>
    /// Adapter để kết nối Application Layer với SignalR Hub
    /// Implement INotificationHub interface để Application có thể sử dụng
    /// Inject INotificationHubContext để gọi các method thực tế của SignalR
    /// </summary>
    public class NotificationHubAdapter : INotificationHub
    {
        private readonly INotificationHubContext _hubContext;

        public NotificationHubAdapter(INotificationHubContext hubContext)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        /// <summary>
        /// Gửi notification cho tất cả clients
        /// </summary>
        public async Task SendNotification(string message)
        {
            await _hubContext.SendNotificationAsync(message);
        }

        /// <summary>
        /// Gửi notification cho client cụ thể
        /// </summary>
        public async Task SendToClient(string connectionId, string message)
        {
            await _hubContext.SendToClientAsync(connectionId, message);
        }

        /// <summary>
        /// Gửi notification cho nhóm clients
        /// </summary>
        public async Task SendToGroup(string groupName, string message)
        {
            await _hubContext.SendToGroupAsync(groupName, message);
        }

        /// <summary>
        /// Gửi notification cho user cụ thể
        /// </summary>
        public async Task SendToUser(string userId, string message)
        {
            await _hubContext.SendToUserAsync(userId, message);
        }
    }
}
