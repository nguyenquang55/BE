using Application.Contracts.Auth.Response;
using Application.DTOs.Auth.Request;
using Application.DTOs.Auth.Response;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface IAuthService
    {
        Task<Result<bool>> ValidateRefreshTokenAsync(string refreshToken, string? sessionId = null, CancellationToken ct = default);
        Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
        Task<Result<string>> LogoutAsync(string? sessionToken = null, CancellationToken ct = default);
        Task<Result<LoginResponse>> RefreshTokenAsync(string refreshToken,string sessionToken,CancellationToken ct = default);
        Task<Result<RegisterRespone>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    }

}
