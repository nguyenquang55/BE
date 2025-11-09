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
        private readonly IWsService _wsService;

        public WsController(IWsService wsService)
        {
            _wsService = wsService;
        }

        [HttpPost]
        public async Task<IActionResult> ValidateExchangeRequest([FromBody] string? sessionToken = null, CancellationToken ct = default)
        {
            return await HandleAsync(_wsService.ValidateWsRequest(sessionToken,HttpContext));
        }
    }
}
