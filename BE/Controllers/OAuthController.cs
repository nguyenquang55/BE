using Application.Abstractions.Services;
using Application.Contracts.OAuth;
using BE.Controllers.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using System.Threading.Tasks;

namespace BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OAuthController : BaseController
    {
        private readonly ILogger<OAuthController> _logger;
        private readonly IOAuthProviderService _OAuthProviderService;
        public OAuthController(ILogger<OAuthController> logger, IOAuthProviderService OAuthProviderService)
        {
            _logger = logger;
            _OAuthProviderService = OAuthProviderService;
        }

        [HttpGet]
        public async Task<IActionResult> OAuth([FromQuery] OAuthRequest oAuthRequest, CancellationToken ct)
        {
            var urlResult = await _OAuthProviderService.CreateAuthorizationUrlAsync(oAuthRequest.SessionToken);
            if (!urlResult.Success)
            {
                _logger.LogWarning("Failed to create authorization URL: {Error}", urlResult.ErrorCode ?? urlResult.Message);
                return StatusCode((int)urlResult.StatusCode, urlResult);
            }

            _logger.LogInformation("Redirecting to authorization endpoint: {Url}", urlResult.Data);
            return Redirect(urlResult.Data);
        }
        [HttpGet("callback")]
        public async Task<IActionResult> OAuthCallback([FromQuery] string code, [FromQuery] string state, CancellationToken ct)
        {
            return await HandleAsync(_OAuthProviderService.HandleCallbackAsync(code, state));
        }
    }
}
