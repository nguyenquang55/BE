namespace Application.Contracts.SignalR
{
    public class WsInfoResponse
    {
        public string Url { get; set; } = string.Empty; // full ws/wss url
        public string Path { get; set; } = "/hubs/notifications"; // hub path
    }
}
