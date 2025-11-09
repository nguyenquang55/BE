using Application.Abstractions.Infrastructure;
using Application.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace Application.Service
{
    public class GenOTPService : IGenOTPService
    {
        private readonly IRedisCacheService _redisCacheService;
        private readonly IConfiguration _configuration;

        public GenOTPService(IRedisCacheService redisCacheService, IConfiguration configuration)
        {
            _redisCacheService = redisCacheService;
            _configuration = configuration;
        }

        public async Task<object> GenerateOTP(string userEmail)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
                throw new ArgumentException("Email is required.", nameof(userEmail));

            // normalize email to avoid key mismatches due to casing/whitespace
            var normalizedEmail = userEmail.Trim().ToLowerInvariant();

            var ttlMinutes = int.TryParse(_configuration["Otp:TtlMinutes"], out var ttl) && ttl > 0 ? ttl : 5;
            var expiry = TimeSpan.FromMinutes(ttlMinutes);

            var redisKey = $"OTP_{normalizedEmail}";

            try
            {
                var existing = await _redisCacheService.GetAsync<string>(redisKey);
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    await _redisCacheService.SetAsync(redisKey, existing, expiry);
                    Console.WriteLine($"[OTP] Reusing existing OTP for {normalizedEmail}");
                    return existing;
                }
            }
            catch
            {
                
            }

            // use cryptographic RNG for OTP generation
            var otpNum = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000);
            var otpStr = otpNum.ToString("D6");

            await _redisCacheService.SetAsync(redisKey, otpStr, expiry);

            Console.WriteLine($"[OTP] Created new OTP for {normalizedEmail}, expires in {ttlMinutes} min");

            return otpStr;
        }

    }
}
