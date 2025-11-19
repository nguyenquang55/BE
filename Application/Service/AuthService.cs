using Application.Abstractions.Common;
using Application.Abstractions.Infrastructure;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Contracts.Auth.Response;
using Application.DTOs.Auth.Request;
using Application.DTOs.Auth.Response;
using Domain.Entities.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Asn1.Ocsp;
using Shared.Common;
using StackExchange.Redis;
using System.Net;
using System.Security.Claims;

namespace Application.Service
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtProvider _jwtProvider;
        private readonly IRedisCacheService _redis;
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _userRepository;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly IContactRepository _contactRepository;
        private readonly IValidator<RegisterRequest> _registerValidator;
        private readonly IValidator<LoginRequest> _loginValidator;
        private readonly IOAuthRepository _oAuthRepository;

        public AuthService(
            IOAuthRepository oAuthRepository,
            IAuthRepository authRepository,
            IUserRepository userRepository,
            ISessionRepository sessionRepository,
            IUnitOfWork unitOfWork,
            IRedisCacheService redis,
            IConfiguration configuration,
            IJwtProvider jwtProvider,
            IValidator<RegisterRequest> registerValidator,
            IValidator<LoginRequest> loginValidator,
            IContactRepository contactRepository)
        {
            _oAuthRepository = oAuthRepository ?? throw new ArgumentNullException(nameof(oAuthRepository));
            _authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _jwtProvider = jwtProvider ?? throw new ArgumentNullException(nameof(jwtProvider));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _passwordHasher = new PasswordHasher<User>();
            _registerValidator = registerValidator ?? throw new ArgumentNullException(nameof(registerValidator));
            _loginValidator = loginValidator ?? throw new ArgumentNullException(nameof(loginValidator));
            _contactRepository = contactRepository ?? throw new ArgumentNullException(nameof(contactRepository));
        }

        public async Task<Result<RegisterRespone>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            var validation = await _registerValidator.ValidateAsync(request, ct);
            if (!validation.IsValid)
                return Result<RegisterRespone>.FailureResult(validation.Errors.First().ErrorMessage, null, HttpStatusCode.BadRequest);

            var emailNormalized = request.Email.Trim().ToLowerInvariant();
            var existingUser = await _authRepository.GetUserByEmail(emailNormalized);
            if (existingUser is not null)
                return Result<RegisterRespone>.FailureResult("Email đã tồn tại trong hệ thống", null, HttpStatusCode.Conflict);

            var user = new User
            {
                Email = request.Email,
                DisplayName = request.DisplayName,
                Timezone = "Asia/HaNoi",
                IsActive = true
            }; 

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            //await _authRepository.AddAsync(user);
            await _redis.RemoveAsync($"Email_{user.Email}");
            await _redis.SetAsync($"Email_{user.Email}", user, TimeSpan.FromMinutes(5));
            //await _unitOfWork.SaveChangesAsync();

            var response = new RegisterRespone();

            return Result<RegisterRespone>.SuccessResult(response, "Đăng ký thành công", HttpStatusCode.OK);
        }

        public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            var validation = await _loginValidator.ValidateAsync(request, ct);
            if (!validation.IsValid)
                return Result<LoginResponse>.FailureResult(validation.Errors.First().ErrorMessage, null, HttpStatusCode.BadRequest);

            var user = await _authRepository.GetUserByEmail(request.Email);
            if (user is null)
                return Result<LoginResponse>.FailureResult("Sai email hoặc mật khẩu", null, HttpStatusCode.Unauthorized);

            var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password);
            if (verify == PasswordVerificationResult.Failed)
                return Result<LoginResponse>.FailureResult("Sai email hoặc mật khẩu", null, HttpStatusCode.Unauthorized);

            if (!user.IsActive)
                return Result<LoginResponse>.FailureResult("Tài khoản bị khóa hoặc chưa kích hoạt", null, HttpStatusCode.Forbidden);

            try
            {
                await _sessionRepository.DisableSessionAsync(user.Id,"New Season Provided");
            }
            catch (Exception ex)
            {
                return Result<LoginResponse>.FailureResult("Đăng nhập thất bại", ex.Message, HttpStatusCode.InternalServerError);
            }


            string sessionToken = await _jwtProvider.GenerateSessionToken(user);
            await _sessionRepository.AddAsync(new Session
            {
                UserId = user.Id,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddMinutes(double.Parse(_configuration["Session:ExpireTimeMinutes"] ?? throw new InvalidOperationException("Invalid format"))),
                SessionToken = sessionToken
            });
             
            await _redis.SetAsync($"UserID:{sessionToken}", user.Id, TimeSpan.FromMinutes(double.Parse(_configuration["Session:ExpireTimeMinutes"] ?? throw new InvalidOperationException("Invalid format"))));

            var userWithProviders = await _userRepository.GetWithAuthProvidersAsync(user.Id, ct) ?? user;
            var userDetail = new UserDetailDTO
            {
                Email = user.Email,
                DisplayName = user.DisplayName,
                Timezone = user.Timezone,
                IsActive = user.IsActive,
                AuthProviders = (userWithProviders.AuthProviders ?? new List<OAuthProvider>())
                    .Select(p => new OAuthProviderDTO
                    {
                        Provider = p.Provider,
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
                User = userDetail,
            };
            var oauthRefreshToken = await _redis.GetAsync<string>($"OAuthRefreshToken:{user.Id}");
            if (string.IsNullOrEmpty(oauthRefreshToken))
            {
                oauthRefreshToken = await _oAuthRepository.GetOAuthTokenAsync(user.Id.ToString());
                await _redis.SetAsync($"OAuthRefreshToken:{user.Id}", oauthRefreshToken, TimeSpan.FromDays(30));
            }


            await _unitOfWork.SaveChangesAsync();

            // Seed user's contacts into Redis cache after successful login
            try
            {
                var allContacts = await _contactRepository.ListAllAsync(user.Id, ct);
                var contactDtos = allContacts.Select(c => new Application.Contracts.Contact.ContactDTO
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    Name = c.Name,
                    Email = c.Email,
                    Source = c.Source
                }).OrderBy(x => x.Name).ToList();
                await _redis.SetAsync($"Contacts:{user.Id}", contactDtos, TimeSpan.FromDays(7));
            }
            catch { /* best-effort cache warmup */ }
            return Result<LoginResponse>.SuccessResult(res, "Đăng nhập thành công", HttpStatusCode.OK);
        }


        public Task<Result<LoginResponse>> RefreshTokenAsync(string refreshToken,string sessionToken,CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
                return Task.FromResult(Result<LoginResponse>.FailureResult("Session token is required", "VALIDATION_ERROR", System.Net.HttpStatusCode.BadRequest));

            if (string.IsNullOrWhiteSpace(refreshToken))
                return Task.FromResult(Result<LoginResponse>.FailureResult("Refresh token không hợp lệ", null, HttpStatusCode.BadRequest));

            return Task.FromResult(Result<LoginResponse>.FailureResult("Chức năng làm mới token chưa được hỗ trợ", "NOT_IMPLEMENTED", HttpStatusCode.NotImplemented));
        }

        public async Task<Result<string>> LogoutAsync(string? sessionToken = null, CancellationToken ct = default)
        {
            try
            {
                var session = await _sessionRepository.GetSessionByToken(sessionToken!, ct: ct);
                if (session == null)
                {
                    return Result<string>.FailureResult("Không tìm thấy session", null, HttpStatusCode.BadRequest);
                }
                else
                {
                    await _redis.RemoveAsync($"UserID:{sessionToken}");
                    await _sessionRepository.DisableSessionAsync(session.UserId, "User Logged Out", ct);
                    return Result<string>.SuccessResult("Đã đăng xuất", "Success", HttpStatusCode.OK);
                }
            }
            catch (Exception ex)
            {
                return Result<string>.FailureResult("Đăng xuất thất bại", ex.Message, HttpStatusCode.InternalServerError);
            }
        }

        public Task<Result<bool>> ValidateRefreshTokenAsync(string refreshToken, string? sessionId = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return Task.FromResult(Result<bool>.FailureResult("Refresh token không hợp lệ", null, HttpStatusCode.BadRequest));

            return Task.FromResult(Result<bool>.FailureResult("Xác thực refresh token chưa được hỗ trợ", "NOT_IMPLEMENTED", HttpStatusCode.NotImplemented));
        }
    }
}
