using Application.Contracts.SignalR;
using Application.Abstractions.Services;
using BE.Controllers.Common;
using Microsoft.AspNetCore.Mvc;

namespace BE.Controllers
{
    [ApiController]
    [Route("api/ws")]
    public class WsController : BaseController
    {
        private readonly ISessionService _sessions;

        public WsController(ISessionService sessions)
        {
            _sessions = sessions;
        }

        [HttpPost("authorize")]
        public async Task<IActionResult> Authorize([FromBody] string? sessionToken = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return BadRequest(new WsAuthorizeResponse
                {
                    Allowed = false,
                    Message = "sessionToken is required"
                });
            }

            var sessionRes = await _sessions.GetSessionByTokenAsync(sessionToken, ct);
            if (!sessionRes.Success || sessionRes.Data == null)
            {
                return Unauthorized(new WsAuthorizeResponse
                {
                    Allowed = false,
                    Message = sessionRes.Message ?? "Unauthorized"
                });
            }

            var req = HttpContext.Request;
            var wsScheme = req.Scheme == "https" ? "wss" : "ws";
            var hubPath = "/hubs/notifications";
            var url = $"{wsScheme}://{req.Host}{hubPath}?sessionToken={Uri.EscapeDataString(sessionToken)}";

            return Ok(new WsAuthorizeResponse
            {
                Allowed = true,
                Message = "Authorized",
                Url = url,
                Path = hubPath,
                UserId = sessionRes.Data.UserId
            });
        }
    }
}
