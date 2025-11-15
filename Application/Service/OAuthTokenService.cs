using Application.Abstractions.Infrastructure;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using Application.Contracts.OAuth;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Application.Service
{
    public class OAuthTokenService : IOAuthTokenService
    {
        private readonly IRedisCacheService _redisCacheService;
        private readonly IConfiguration _configuration;
        private readonly IOAuthRepository _oAuthRepository;

        public OAuthTokenService(
            IRedisCacheService redisCacheService,
            IConfiguration configuration,
            IOAuthRepository oAuthRepository)
        {
            _redisCacheService = redisCacheService;
            _configuration = configuration;
            _oAuthRepository = oAuthRepository;
        }

        public async Task<string> GetAccessToken(Guid userId)
        {
            var key = $"OAuthAccessToken:{userId}";
            var accessToken = await _redisCacheService.GetAsync<string>(key);
            if (!string.IsNullOrWhiteSpace(accessToken))
                return accessToken;

            return await RefreshAccessToken(userId);
        }

        public async Task<string> RefreshAccessToken(Guid userId)
        {
            var refreshToken = await _redisCacheService.GetAsync<string>($"OAuthRefreshToken:{userId}");

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                refreshToken = await _oAuthRepository.GetOAuthTokenAsync(userId.ToString());
                if (!string.IsNullOrWhiteSpace(refreshToken))
                {
                    await _redisCacheService.SetAsync($"OAuthRefreshToken:{userId}", refreshToken, TimeSpan.FromDays(30));
                }
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
                return string.Empty;

            var tokenEndpoint = _configuration["OAuth:Google:TokenEndpoint"] ?? "https://oauth2.googleapis.com/token";
            var clientId = _configuration["OAuth:Google:ClientId"];
            var clientSecret = _configuration["OAuth:Google:ClientSecret"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return string.Empty;

            using var http = new HttpClient();
            var req = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("client_id", clientId),
                new KeyValuePair<string,string>("client_secret", clientSecret),
                new KeyValuePair<string,string>("grant_type", "refresh_token"),
                new KeyValuePair<string,string>("refresh_token", refreshToken),
            });

            var resp = await http.PostAsync(tokenEndpoint, req);
            if (!resp.IsSuccessStatusCode)
                return string.Empty;

            var content = await resp.Content.ReadAsStringAsync();
            TokenResponse? token;
            try
            {
                token = JsonSerializer.Deserialize<TokenResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return string.Empty;
            }

            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
                return string.Empty;

            await _redisCacheService.SetAsync($"OAuthAccessToken:{userId}", token.AccessToken, TimeSpan.FromSeconds(token.ExpiresIn));

            return token.AccessToken;
        }
    }
}