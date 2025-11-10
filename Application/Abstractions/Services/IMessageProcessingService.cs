using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    /// <summary>
    /// Xử lý message bất đồng bộ cho WebSocket/SignalR.
    /// Trả về nội dung đã xử lý (ví dụ chuẩn hoá, phân tích, ...).
    /// </summary>
    public interface IMessageProcessingService
    {
        /// <summary>
        /// Thực hiện xử lý business cho nội dung thô.
        /// </summary>
        /// <param name="raw">Nội dung client gửi.</param>
        /// <returns>Kết quả đã xử lý.</returns>
        Task<string> ProcessAsync(string raw);
    }
}
