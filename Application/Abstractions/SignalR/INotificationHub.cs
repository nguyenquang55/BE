using System.Threading.Tasks;

namespace Application.Abstractions.SignalR
{
    /// <summary>
    /// Interface cho Notification Hub Adapter - được sử dụng bởi Application Layer
    /// Chịu trách nhiệm gọi các method từ Infrastructure để gửi notification
    /// </summary>
    public interface INotificationHub
    {
        /// <summary>
        /// Gửi notification cho tất cả clients
        /// </summary>
        Task SendNotification(string message);

        /// <summary>
        /// Gửi notification cho client cụ thể theo ConnectionId
        /// </summary>
        Task SendToClient(string connectionId, string message);

        /// <summary>
        /// Gửi notification cho nhóm clients
        /// </summary>
        Task SendToGroup(string groupName, string message);

        /// <summary>
        /// Gửi notification cho user cụ thể theo UserId
        /// </summary>
        Task SendToUser(string userId, string message);
    }
}
