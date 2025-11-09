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
                    await _redisCacheService.SetAsync($"OAuth:Google:Accesstoken:{userId}",token.access_token , TimeSpan.FromSeconds(token.expires_in));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to deserialize token response: " + ex.Message);
                    return Result<string>.FailureResult("Invalid token response", string.Empty, System.Net.HttpStatusCode.BadRequest);
                }
            }

            if (token == null || string.IsNullOrEmpty(token.access_token))
                return Result<string>.FailureResult("Invalid token response", string.Empty, System.Net.HttpStatusCode.BadRequest);
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
                    // ignore decoding errors
                }
            }

            if (string.IsNullOrEmpty(providerUserId))
            {
                return Result<string>.FailureResult("Cannot determine provider user id (sub).", string.Empty, System.Net.HttpStatusCode.BadRequest);
            }

            // Check if already linked; if yes, inform and do not save
            var currentUserId = Guid.Parse(userId);
            var providerEmail = userInfo?.Email ?? string.Empty;

            try
            {
                // If this email is linked by another account -> conflict
                if (!string.IsNullOrWhiteSpace(providerEmail))
                {
                    var linkedByOther = await _oAuthProviderRepository.IsEmailLinkedByOtherAsync(currentUserId, providerName, providerEmail);
                    if (linkedByOther)
                    {
                        var payload = JsonSerializer.Serialize(new { status = "email-linked-by-other", email = providerEmail });
                        return Result<string>.FailureResult("Email đã được liên kết bởi tài khoản khác", payload, System.Net.HttpStatusCode.Conflict);
                    }
                }

                // If this user already linked this provider account -> just inform
                var alreadyLinked = await _oAuthProviderRepository.IsLinkedForUserAsync(currentUserId, providerName, providerEmail, providerUserId);
                if (alreadyLinked)
                {
                    var payload = JsonSerializer.Serialize(new { status = "already-linked", email = providerEmail });
                    return Result<string>.SuccessResult(payload, "Tài khoản đã được liên kết trước đó", System.Net.HttpStatusCode.OK);
                }

                // Save new linkage and token
                OAuthProvider oAuthProvider = new OAuthProvider
                {
                    UserId = currentUserId,
                    Provider = providerName,
                    ProviderUserId = providerUserId,
                    ProviderEmail = providerEmail,
                    DisplayName = userInfo?.Name,
                };
                await _oAuthProviderRepository.AddAsync(oAuthProvider);

                await _oAuthTokenRepository.AddAsync(new OAuthToken
                {
                    UserId = oAuthProvider.UserId,
                    AuthProviderId = oAuthProvider.Id,
                    Scopes = token.scope,
                    RefreshToken = token.refresh_token,
                });
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving OAuth data: " + ex.Message);
                return Result<string>.FailureResult("Failed to save OAuth data", string.Empty, System.Net.HttpStatusCode.InternalServerError);
            }

            var resultPayload = JsonSerializer.Serialize(new
            {
                status = "good" ,
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
