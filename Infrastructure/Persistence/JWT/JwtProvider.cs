using Application.Abstractions.Infrastructure;
using Domain.Entities.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Persistence.JWT
{
    public class JwtProvider : IJwtProvider
    {
        private readonly IConfiguration _config;
        private readonly byte[] _keyBytes;
        private readonly string? _issuer;
        private readonly string? _audience;
        private readonly int _accessTokenMinutes;

        public JwtProvider(IConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _keyBytes = Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured"));
            _issuer = _config["Jwt:Issuer"];
            _audience = _config["Jwt:Audience"];
            if (!int.TryParse(_config["Jwt:AccessTokenExpirationMinutes"], out _accessTokenMinutes))
                _accessTokenMinutes = 15;
        }

        public Task<string> GenerateAccessToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var signingKey = new SymmetricSecurityKey(_keyBytes);
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(_accessTokenMinutes),
                signingCredentials: creds
            );

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
            return Task.FromResult(accessToken);
        }

        public Task<string> GenerateRefreshToken(User user)
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            var token = Convert.ToBase64String(randomNumber);
            return Task.FromResult(token);
        }

        public Task<string> GenerateSessionToken(User user)
        {
            var claims = new[]
           {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Session:key"]));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(double.Parse(_config["Session:ExpireTimeMinutes"])),
                signingCredentials: creds
            );

            var SessionToken = new JwtSecurityTokenHandler().WriteToken(token);
            return Task.FromResult(SessionToken);
        }

        public async Task<(string AccessToken, string RefreshToken)> GenerateToken(User user)
        {
            var access = await GenerateAccessToken(user);
            var refresh = await GenerateRefreshToken(user);
            return (AccessToken: access, RefreshToken: refresh);
        }
    }
}
