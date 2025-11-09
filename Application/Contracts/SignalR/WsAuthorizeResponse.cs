using System;

namespace Application.Contracts.SignalR
{
    public class WsAuthorizeResponse
    {
        public bool Allowed { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Path { get; set; } = "/hubs/notifications";
        public Guid? UserId { get; set; }
    }
}
