using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    /// <summary>
    /// Xử lý message bất đồng bộ cho WebSocket/SignalR.
    /// Trả về nội dung đã xử lý (ví dụ chuẩn hoá, phân tích, ...).
    /// </summary>
    public interface IMessageProcessingService
    {
        Task<string> ProcessAsync(string raw);
    }
}
