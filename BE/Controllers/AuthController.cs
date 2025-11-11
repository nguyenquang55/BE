using Application.Abstractions.Services;
using Application.Contracts.Auth.Request;
using Application.Contracts.Auth.Response;
using Application.DTOs.Auth.Request;
using Application.DTOs.Auth.Response;
using BE.Controllers.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using System.Security.Claims;

namespace BE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseController
    {
        private readonly IAuthService _auth;
        private readonly IValidateEmailService _validateEmailService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService auth, ILogger<AuthController> logger, IValidateEmailService validateEmailService)
        {
            _auth = auth;
            _logger = logger;
            _validateEmailService = validateEmailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
        {
            return await HandleAsync(_auth.RegisterAsync(request, ct));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            return await HandleAsync(_auth.LoginAsync(request, ct));
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshAuthTokenRequest request, CancellationToken ct)
        {
            return await HandleAsync(_auth.RefreshTokenAsync(request.RefreshToken, request.SessionToken ?? string.Empty, ct));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromQuery] string? sessionToken = null, CancellationToken ct = default)
        {
            return await HandleAsync(_auth.LogoutAsync(sessionToken, ct));
        }

        [HttpPost("validate-refresh")]
        public async Task<IActionResult> ValidateRefresh([FromBody] RefreshAuthTokenRequest request, [FromQuery] string? sessionId, CancellationToken ct)
        {
            return await HandleAsync(_auth.ValidateRefreshTokenAsync(request.RefreshToken, sessionId, ct));
        }

        [HttpPost("SendOTP")]
        public async Task<IActionResult> SendOTP([FromBody] SendOtpRequest request, CancellationToken ct)
        {
            return await HandleAsync(_validateEmailService.SendOtpAsync(request.Email, "Register", ct));
        }
        [HttpPost("VerifyOTP")]
        public async Task<IActionResult> VerifyOTP([FromBody] VerifyOtpRequest request, CancellationToken ct)
        {
            return await HandleAsync(_validateEmailService.VerifyOtpAsync(request.Email, request.Code, "Register", ct));
        }
    }
}
