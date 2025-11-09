using Application.Abstractions.SignalR;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace BE.Hubs
{
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var http = Context.GetHttpContext();
            var token = http?.Request.Query["sessionToken"].ToString();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WS CONNECT] ConnId={Context.ConnectionId} Token={token}");
            Console.ResetColor();
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[WS DISCONNECT] ConnId={Context.ConnectionId} Reason={exception?.Message}");
            Console.ResetColor();
            await base.OnDisconnectedAsync(exception);
        }

        public Task Echo(string message)
        {
            var http = Context.GetHttpContext();
            var token = http?.Request.Query["sessionToken"].ToString();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[WS MESSAGE] ConnId={Context.ConnectionId} Token={token} Message={message}");
            Console.ResetColor();
            return Clients.Caller.SendAsync("echo", message);
        }
    }
}
