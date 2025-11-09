using System.Threading.Tasks;

namespace Application.Abstractions.SignalR
{
    /// <summary>
    /// Interface cho NotificationHub concrete class - được implement bởi SignalR Hub
    /// Chịu trách nhiệm thực hiện các method SignalR thực tế
    /// Infrastructure layer sử dụng interface này để inject vào Adapter
    /// </summary>
    public interface INotificationHubContext
    {
        /// <summary>
        /// Gửi notification cho tất cả clients (async)
        /// </summary>
        Task SendNotificationAsync(string message);

        /// <summary>
        /// Gửi notification cho client cụ thể theo ConnectionId (async)
        /// </summary>
        Task SendToClientAsync(string connectionId, string message);

        /// <summary>
        /// Gửi notification cho nhóm clients (async)
        /// </summary>
        Task SendToGroupAsync(string groupName, string message);

        /// <summary>
        /// Gửi notification cho user cụ thể theo UserId (async)
        /// </summary>
        Task SendToUserAsync(string userId, string message);
    }
}
