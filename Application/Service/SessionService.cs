using Application.Abstractions.Common;
using Application.Abstractions.Infrastructure;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Contracts.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Application.Service
{
    public class SessionService : ISessionService
    {
        private readonly IUnitOfWork _uow;
        private readonly ISessionRepository _sessionRepository;
        private readonly IRedisCacheService _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SessionService> _logger;
        private readonly TimeSpan _sessionTtl;

        public SessionService(
            IUnitOfWork uow,
            ISessionRepository sessionRepository,
            IRedisCacheService cache,
            IConfiguration configuration,
            ILogger<SessionService> logger
        )
        {
            _uow = uow;
            _sessionRepository = sessionRepository;
            _cache = cache;
            _logger = logger;
            _sessionTtl = TimeSpan.FromDays(30); // or read from config
        }

        private static string ComputeSha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }


        public Task<Result<SessionDTO>> CreateSessionAsync(Guid userId, string refreshPlain, string? deviceId, string? ip, string? userAgent, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<SessionDTO>> CreateSessionAsync(Guid userId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<SessionDTO?>> GetSessionByRefreshHashAsync(string refreshHash, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<SessionDTO?>> GetSessionByTokenAsync(string sessionToken, CancellationToken ct = default)
        {
            var session = await _sessionRepository.GetSessionByToken(sessionToken, ct);
            if (session == null || session.IsRevoked == true)
            {
                return Result<SessionDTO?>.FailureResult("Session không hợp lệ hoặc đã hết hạn", null, System.Net.HttpStatusCode.Unauthorized);
            }

            var sessionDto = new SessionDTO
            {
                SessionId = session.Id,
                UserId = session.UserId,
                SessionToken = session.SessionToken ?? string.Empty,
                ExpireAt = session.ExpiresAt ?? DateTimeOffset.MinValue,
                IsRevoked = session.IsRevoked,
                DeviceId = null, // Set appropriately if available
                ipAddress = string.Empty, // Set appropriately if available
                userAgent = string.Empty // Set appropriately if available
            };

            return Result<SessionDTO?>.SuccessResult(sessionDto, "Session hợp lệ", System.Net.HttpStatusCode.OK);
        }

        public Task<bool> IsSessionValidAsync(string sessionToken, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<SessionDTO>> ListSessionsForUserAsync(Guid userId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<SessionDTO?>> RefreshSessionAsync(string sessionToken, string oldRefreshPlain, string newRefreshPlain, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<SessionDTO?>> RefreshSessionAsync(string sessionToken, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RevokeSessionAsync(string sessionToken, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
