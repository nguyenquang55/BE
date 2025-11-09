using Application.Abstractions.Common;
using Application.Abstractions.Infrastructure;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.DTOs.Auth.Response;
using Domain.Entities.Identity;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Service
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ISessionService _sessionService;

        public UserService(IUserRepository userRepository, ISessionService sessionService)
        {
            _userRepository = userRepository;
            _sessionService = sessionService;
        }

        public async Task<Result<LoginResponse>> Refresh(string sessionToken, CancellationToken ct = default)
        {

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return Result<LoginResponse>.FailureResult(
                    "Phiên đăng nhập không hợp lệ hoặc đã hết hạn",
                    "INVALID_SESSION",
                    HttpStatusCode.Unauthorized);
            }

            var sessionResult = await _sessionService.GetSessionByTokenAsync(sessionToken, ct);
            if (sessionResult is null || !sessionResult.Success || sessionResult.Data is null)
            {
                return Result<LoginResponse>.FailureResult(
                    "Phiên đăng nhập không hợp lệ hoặc đã hết hạn",
                    "INVALID_SESSION",
                    HttpStatusCode.Unauthorized);
            }

            var session = sessionResult.Data;

            try
            {
                await _sessionService.RefreshSessionAsync(sessionToken, ct);
            }
            catch
            {
            }

            var user = await _userRepository.GetWithAuthProvidersAsync(session.UserId, ct);
            if (user is null)
            {
                return Result<LoginResponse>.FailureResult(
                    "Không tìm thấy người dùng",
                    "USER_NOT_FOUND",
                    HttpStatusCode.NotFound);
            }

            var userDetail = new UserDetailDTO
            {
                UserId = session.UserId,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Timezone = user.Timezone,
                IsActive = user.IsActive,
                AuthProviders = (user.AuthProviders ?? new List<OAuthProvider>())
                    .Select(p => new OAuthProviderDTO
                    {
                        Id = p.Id,
                        Provider = p.Provider,
                        ProviderUserId = p.ProviderUserId,
                        ProviderEmail = p.ProviderEmail,
                        DisplayName = p.DisplayName,
                        IsPrimary = p.IsPrimary,
                        LinkedAt = p.LinkedAt
                    })
                    .ToList()
            };

            var res = new LoginResponse
            {
                sessionToken = sessionToken,
                User = userDetail
            };

            return Result<LoginResponse>.SuccessResult(res, "Done", HttpStatusCode.OK);
        }
    }
}
