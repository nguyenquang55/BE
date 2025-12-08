using Application.Abstractions.Common;
using Application.Abstractions.Infrastructure;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Domain.Entities.Identity;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Application.Service
{
    public class OAuthProviderService : IOAuthProviderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISessionService _sessionService;
        private readonly IConfiguration _configuration;
        private readonly IRedisCacheService _redisCacheService;
        private readonly IOAuthProviderRepository _oAuthProviderRepository;
        private readonly IOAuthTokenRepository _oAuthTokenRepository;
        //private readonly IDataProtector _protector;
        //private string? _userId;

        public OAuthProviderService(
            IUnitOfWork unitOfWork,
            IOAuthTokenRepository oAuthTokenRepository,
            IRedisCacheService redisCacheService,
            ISessionService sessionService,
            IOAuthProviderRepository oAuthProviderRepository,
            IConfiguration configuration
        )
        //IDataProtectionProvider dataProtectionProvider) // need to register IDataProtection in DI
        {
            _unitOfWork = unitOfWork;
            _oAuthTokenRepository = oAuthTokenRepository;
            _redisCacheService = redisCacheService;
            _oAuthProviderRepository = oAuthProviderRepository;
            _sessionService = sessionService;
            _configuration = configuration;
            //_protector = dataProtectionProvider.CreateProtector("OAuthRefreshTokenProtector");
        }

        private async Task<string?> ValidateClientAsync(string SessionToken)
        {
            var sessionResult = await _sessionService.GetSessionByTokenAsync(SessionToken);
            if (!sessionResult.Success || sessionResult.Data == null)
            {
                return null;
            }
            string? _userId = sessionResult.Data?.UserId.ToString();
            return _userId;
        }

        public async Task<Result<string>> CreateAuthorizationUrlAsync(string sessionToken)
        {
            string? userId = await ValidateClientAsync(sessionToken);
            if (string.IsNullOrEmpty(userId))
            {
                return Result<string>.FailureResult("User not authenticated", string.Empty, System.Net.HttpStatusCode.Unauthorized);
            }

            var clientId = _configuration["OAuth:Google:ClientId"];
            var redirectUri = _configuration["OAuth:Google:RedirectUri"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
            {
                return Result<string>.FailureResult("OAuth client configuration missing", "CONFIG_MISSING", System.Net.HttpStatusCode.InternalServerError);
            }

            var emailScope = _configuration["OAuth:Google:Scopes:Email"];
            var calendarScope = _configuration["OAuth:Google:Scopes:Calendar"];

            var scopeList = new[] { "openid", "email", "profile", emailScope, calendarScope }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .Distinct()
                .ToArray();

            var scope = string.Join(" ", scopeList);

            // Force Google account chooser even if there's an active Google session in the browser
            // Also consider adding consent to ensure refresh_token issuance on re-link
            var prompt = "select_account"; // or "consent select_account" if you need re-consent each time

            var url = $"https://accounts.google.com/o/oauth2/v2/auth" +
                      $"?scope={HttpUtility.UrlEncode(scope)}" +
                      $"&access_type=offline" +
                      $"&include_granted_scopes=true" +
                      $"&prompt={HttpUtility.UrlEncode(prompt)}" +
                      $"&response_type=code" +
                      $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
                      $"&client_id={HttpUtility.UrlEncode(clientId)}" +
                      $"&state={HttpUtility.UrlEncode(sessionToken)}";

            return Result<string>.SuccessResult(url, "Authorization URL created", System.Net.HttpStatusCode.OK);
        }

        public async Task<Result<string>> HandleCallbackAsync(string code, string state)
        {
            if (string.IsNullOrEmpty(code))
                return Result<string>.FailureResult("Missing authorization code", string.Empty, System.Net.HttpStatusCode.BadRequest);

            if (string.IsNullOrEmpty(state))
                return Result<string>.FailureResult("Missing state (session token)", string.Empty, System.Net.HttpStatusCode.BadRequest);

            var userId = await ValidateClientAsync(state);
            if (string.IsNullOrEmpty(userId))
                return Result<string>.FailureResult("Invalid session/state or user not found", string.Empty, System.Net.HttpStatusCode.Unauthorized);

            await _redisCacheService.RemoveAsync($"OAuthAccessToken:{userId}");
            await _redisCacheService.RemoveAsync($"OAuth:Google:Accesstoken:{userId}");
            await _redisCacheService.RemoveAsync($"OAuthRefreshToken:{userId}");
            await _oAuthProviderRepository.DisableOAuthAsync(Guid.Parse(userId), "Google");

            var tokenEndpoint = _configuration["OAuth:Google:TokenEndpoint"] ?? "https://oauth2.googleapis.com/token";
            var clientId = _configuration["OAuth:Google:ClientId"];
            var clientSecret = _configuration["OAuth:Google:ClientSecret"];
            var redirectUri = _configuration["OAuth:Google:RedirectUri"];
            var providerName = "Google";

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(redirectUri))
            {
                return Result<string>.FailureResult("OAuth client configuration missing", "CONFIG_MISSING", System.Net.HttpStatusCode.InternalServerError);
            }
            #region Token Handling
            // token handling
            TokenResponse? token = null;
            using (var http = new HttpClient())
            {
                var requestContent = new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code"
                };

                HttpResponseMessage tokenResponse;
                try
                {
                    tokenResponse = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(requestContent));
                }
                catch (Exception ex)
                {
                    return Result<string>.FailureResult("Token exchange failed: " + ex.Message, string.Empty, System.Net.HttpStatusCode.BadRequest);
                }

                var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
                if (!tokenResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("Token endpoint error: " + tokenContent);
                    return Result<string>.FailureResult("Token exchange failed", string.Empty, System.Net.HttpStatusCode.BadRequest);
                }

                try
                {
                    token = JsonSerializer.Deserialize<TokenResponse>(tokenContent);
                    await _redisCacheService.SetAsync($"OAuth:Google:Accesstoken:{userId}", token.access_token, TimeSpan.FromSeconds(token.expires_in));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to deserialize token response: " + ex.Message);
                    return Result<string>.FailureResult("Invalid token response", string.Empty, System.Net.HttpStatusCode.BadRequest);
                }
            }

            if (token == null || string.IsNullOrEmpty(token.access_token))
                return Result<string>.FailureResult("Invalid access token response", string.Empty, System.Net.HttpStatusCode.BadRequest);
            if (token == null || string.IsNullOrEmpty(token.refresh_token))
                return Result<string>.FailureResult("Invalid refresh token response", string.Empty, System.Net.HttpStatusCode.BadRequest);
            #endregion End Token Handling

            #region Email Info
            UserInfoResponse? userInfo = null;
            using (var http = new HttpClient())
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.access_token);

                try
                {
                    var resp = await http.SendAsync(req);
                    var ui = await resp.Content.ReadAsStringAsync();
                    if (resp.IsSuccessStatusCode)
                    {
                        userInfo = JsonSerializer.Deserialize<UserInfoResponse>(ui, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    else
                    {
                        Console.WriteLine("Email Info endpoint error: " + ui);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to call Email info: " + ex.Message);
                }
            }
            #endregion
            string? providerUserId = null;
            if (userInfo != null && !string.IsNullOrEmpty(userInfo.Sub))
            {
                providerUserId = userInfo.Sub;
            }
            else if (!string.IsNullOrEmpty(token.id_token))
            {
                try
                {
                    var parts = token.id_token.Split('.');
                    if (parts.Length >= 2)
                    {
                        var payload = parts[1];
                        var bytes = Base64UrlDecode(payload);
                        var json = Encoding.UTF8.GetString(bytes);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("sub", out var subEl))
                            providerUserId = subEl.GetString();
                    }
                }
                catch
                {

                }
            }

            if (string.IsNullOrEmpty(providerUserId))
            {
                return Result<string>.FailureResult("Cannot determine provider user id (sub).", string.Empty, System.Net.HttpStatusCode.BadRequest);
            }

            var currentUserId = Guid.Parse(userId);
            var providerEmail = userInfo?.Email ?? string.Empty;
            bool isFirstProvider = false;

            try
            {
                if (!string.IsNullOrWhiteSpace(providerEmail))
                {
                    var linkedByOther = await _oAuthProviderRepository.IsEmailLinkedByOtherAsync(currentUserId, providerName, providerEmail);
                    if (linkedByOther)
                    {
                        var payload = JsonSerializer.Serialize(new { status = "email-linked-by-other", email = providerEmail });
                        return Result<string>.FailureResult("Email is already linked by another account", payload, System.Net.HttpStatusCode.Conflict);
                    }
                }

                isFirstProvider = !(await _oAuthProviderRepository.HasAnyProviderAsync(currentUserId, providerName));

                var alreadyLinked = await _oAuthProviderRepository.IsLinkedForUserAsync(currentUserId, providerName, providerEmail, providerUserId);
                if (alreadyLinked)
                {
                    var payload = JsonSerializer.Serialize(new { status = "already-linked", email = providerEmail, prime = false });
                    return Result<string>.SuccessResult(payload, "Tài khoản đã được liên kết trước đó", System.Net.HttpStatusCode.OK);
                }

                OAuthProvider oAuthProvider = new OAuthProvider
                {
                    UserId = currentUserId,
                    Provider = providerName,
                    ProviderUserId = providerUserId,
                    ProviderEmail = providerEmail,
                    DisplayName = userInfo?.Name,
                    IsPrimary = true,
                };
                await _oAuthProviderRepository.AddAsync(oAuthProvider);

                await _oAuthTokenRepository.AddAsync(new OAuthToken
                {
                    UserId = oAuthProvider.UserId,
                    AuthProviderId = oAuthProvider.Id,
                    Scopes = token.scope,
                    RefreshToken = token.refresh_token,
                });

                await _redisCacheService.SetAsync($"OAuthAccessToken:{userId}", token.access_token, TimeSpan.FromSeconds(token.expires_in));
                await _redisCacheService.SetAsync($"OAuthRefreshToken:{userId}", token.refresh_token, TimeSpan.FromDays(30));
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving OAuth data: " + ex.Message);
                return Result<string>.FailureResult("Failed to save OAuth data", string.Empty, System.Net.HttpStatusCode.InternalServerError);
            }

            var resultPayload = JsonSerializer.Serialize(new
            {
                status = "good",
                prime = isFirstProvider
            });

            return Result<string>.SuccessResult(resultPayload, "Callback handled", System.Net.HttpStatusCode.OK);
        }

        public Task<Result<string>> HandleCallbackAsync(string code)
            => HandleCallbackAsync(code, string.Empty);

        private static byte[] Base64UrlDecode(string input)
        {
            string output = input.Replace('-', '+').Replace('_', '/');
            switch (output.Length % 4)
            {
                case 2: output += "=="; break;
                case 3: output += "="; break;
            }
            return Convert.FromBase64String(output);
        }


        public async Task<Result<string>> Refresh(string sessionToken, string providerUserId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(providerUserId))
                return Result<string>.FailureResult("providerUserId is required", "PROVIDER_USER_ID_REQUIRED", System.Net.HttpStatusCode.BadRequest);

            var userIdString = await ValidateClientAsync(sessionToken);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
                return Result<string>.FailureResult("User not authenticated", "UNAUTHORIZED", System.Net.HttpStatusCode.Unauthorized);

            await _redisCacheService.RemoveAsync($"OAuthAccessToken:{userId}"); 
            await _redisCacheService.RemoveAsync($"OAuth:Google:Accesstoken:{userId}");
            await _redisCacheService.RemoveAsync($"OAuthRefreshToken:{userId}");

            const string providerName = "Google";

            var provider = await _oAuthProviderRepository.GetByProviderUserIdAsync(userId, providerName, providerUserId, ct);
            if (provider == null)
                return Result<string>.FailureResult("Provider not found for user", "PROVIDER_NOT_FOUND", System.Net.HttpStatusCode.NotFound);

            await _oAuthProviderRepository.SetPrimaryAsync(userId, providerName, provider.Id, ct);
            await _unitOfWork.SaveChangesAsync();

            var tokenEntity = await _oAuthTokenRepository.GetLatestByProviderAsync(userId, provider.Id, ct);
            if (tokenEntity == null || string.IsNullOrWhiteSpace(tokenEntity.RefreshToken))
                return Result<string>.FailureResult("Refresh token not found for provider", "REFRESH_TOKEN_MISSING", System.Net.HttpStatusCode.NotFound);

            await _redisCacheService.SetAsync($"OAuthRefreshToken:{userId}", tokenEntity.RefreshToken, TimeSpan.FromDays(30));

            var token = await ExchangeRefreshTokenAsync(tokenEntity.RefreshToken, ct);
            if (token == null || string.IsNullOrWhiteSpace(token.access_token))
                return Result<string>.FailureResult("Failed to refresh access token", "TOKEN_EXCHANGE_FAILED", System.Net.HttpStatusCode.BadRequest);

            var expiresInSeconds = token.expires_in > 0 ? token.expires_in : 3600;
            var lifetime = TimeSpan.FromSeconds(expiresInSeconds);

            await _redisCacheService.SetAsync($"OAuthAccessToken:{userId}", token.access_token, lifetime);
            await _redisCacheService.SetAsync($"OAuth:Google:Accesstoken:{userId}", token.access_token, lifetime);

            var payload = JsonSerializer.Serialize(new
            {
                status = "refreshed",
                providerUserId,
                expiresIn = expiresInSeconds
            });

            return Result<string>.SuccessResult(payload, "Primary provider updated and access token refreshed", System.Net.HttpStatusCode.OK);
        }

        private async Task<TokenResponse?> ExchangeRefreshTokenAsync(string refreshToken, CancellationToken ct)
        {
            var tokenEndpoint = _configuration["OAuth:Google:TokenEndpoint"] ?? "https://oauth2.googleapis.com/token";
            var clientId = _configuration["OAuth:Google:ClientId"];
            var clientSecret = _configuration["OAuth:Google:ClientSecret"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return null;

            using var http = new HttpClient();
            var request = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("client_id", clientId),
                new KeyValuePair<string,string>("client_secret", clientSecret),
                new KeyValuePair<string,string>("grant_type", "refresh_token"),
                new KeyValuePair<string,string>("refresh_token", refreshToken)
            });

            HttpResponseMessage response;
            try
            {
                response = await http.PostAsync(tokenEndpoint, request, ct);
            }
            catch
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync(ct);
            try
            {
                return JsonSerializer.Deserialize<TokenResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        private sealed class TokenResponse
        {
            public string access_token { get; set; } = string.Empty;
            public string? refresh_token { get; set; }
            public int expires_in { get; set; }
            public string scope { get; set; } = string.Empty;
            public string token_type { get; set; } = string.Empty;
            public string id_token { get; set; } = string.Empty;
        }

        private sealed class UserInfoResponse
        {
            public string? Sub { get; set; }
            public string? Email { get; set; }
            public string? Name { get; set; }
        }
    }
}
