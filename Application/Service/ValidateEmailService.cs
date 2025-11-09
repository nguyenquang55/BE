using Application.Abstractions.Common;
using Application.Abstractions.Infrastructure;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Contracts.Auth.Request;
using Domain.Entities.Identity;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Common;
using StackExchange.Redis;
using System;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Service
{
    public class ValidateEmailService : IValidateEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<ValidateEmailService> _logger;
        private readonly IAuthRepository _authRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IValidator<VerifyOtpRequest> _validator;
        private readonly IGenOTPService _genOTPBgrService;
        private readonly IRedisCacheService _redis;
        private readonly int _ttlMinutes;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public ValidateEmailService(
            IAuthRepository authRepository,
            IConfiguration config,
            IUnitOfWork unitOfWork,
            ILogger<ValidateEmailService> logger,
            IGenOTPService genOTPService,
            IValidator<VerifyOtpRequest> validator,
            IRedisCacheService redis)
        {
            _config = config;
            _authRepository = authRepository;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _validator = validator;
            _genOTPBgrService = genOTPService;
            _redis = redis;
            _ttlMinutes = int.TryParse(_config["Otp:TtlMinutes"], out var ttl) && ttl > 0 ? ttl : 5;
        }
        // có lỗi ở chỗ gửi email, cần kiểm tra lại
        public async Task<Result<string>> SendOtpAsync(string email, string purpose = "Register", CancellationToken ct = default)
        {
            var pass = _config["Smtp:Pass"];
            var from = _config["Smtp:From"];

            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required.", nameof(email));

            // normalize email consistently with GenOTPService
            var normalizedEmail = email.Trim().ToLowerInvariant();

            var codeObj = await _genOTPBgrService.GenerateOTP(normalizedEmail);
            var code = codeObj?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                var num = RandomNumberGenerator.GetInt32(0, 1_000_000);
                code = num.ToString("D6");
            }

            var mail = new MailMessage()
            {
                From = new MailAddress(from),
                Subject = "Your One-Time Password (OTP)",
                Body = @$"
                <html>
                <body style='font-family:Segoe UI,Arial,sans-serif; background:linear-gradient(135deg,#e3f2fd 0%,#fff 100%); color:#222;'>
                    <div style='max-width:420px; margin:40px auto; padding:32px 28px; background:#fff; border-radius:16px; box-shadow:0 6px 24px rgba(25,118,210,0.10); border:1px solid #e3f2fd;'>
                        <div style='text-align:center; margin-bottom:18px;'>
                            <img src='https://img.icons8.com/color/48/000000/lock--v1.png' alt='OTP' style='width:48px; height:48px;'/>
                        </div>
                        <h2 style='color:#1976d2; text-align:center; font-size:2rem; margin-bottom:12px; letter-spacing:2px;'>Your Secure OTP Code</h2>
                        <p style='font-size:18px; text-align:center; margin:24px 0 8px 0;'>
                            <span style='display:inline-block; background:linear-gradient(90deg,#1976d2 0%,#64b5f6 100%); color:#fff; font-weight:bold; font-size:36px; padding:16px 40px; border-radius:10px; letter-spacing:6px; box-shadow:0 2px 8px rgba(25,118,210,0.10);'>{code}</span>
                        </p>
                        <p style='text-align:center; margin-bottom:20px; font-size:15px; color:#555;'>
                            Please do not share this code with anyone.<br/>
                            <span style='color:#1976d2; font-weight:500;'>It expires in {_ttlMinutes} minutes.</span>
                        </p>
                        <div style='text-align:center; margin:18px 0;'>
                            <span style='display:inline-block; background:#e3f2fd; color:#1976d2; padding:6px 18px; border-radius:6px; font-size:13px; font-style:italic;'>For your security, keep this code confidential.</span>
                        </div>
                        <hr style='border:none; border-top:1px solid #eee; margin:24px 0;'/>
                        <p style='font-size:14px; color:#888; text-align:center; margin-top:12px;'>Best regards,<br/><b>QuangNT</b></p>
                    </div>
                </body>
                </html>
                ",
                IsBodyHtml = true
            };
                mail.To.Add(normalizedEmail);

            using (var smtp = new SmtpClient("smtp.gmail.com", 587))
            {
                smtp.EnableSsl = true;
                smtp.Credentials = new NetworkCredential(from, pass);
                smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                try
                {
                    smtp.Send(mail);
                    return Result<string>.SuccessResult("good","OTP sent successfully.", HttpStatusCode.OK);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send OTP email to {Email}", email);
                    return Result<string>.FailureResult("Failed to send OTP email.", "EMAIL_SEND_FAILED", HttpStatusCode.InternalServerError);
                }
            }
        }

        public async Task<Result<bool>> VerifyOtpAsync(string email, string code, string purpose = "Register", CancellationToken ct = default)
        {
            var validation = await _validator.ValidateAsync(new VerifyOtpRequest { Email = email, Code = code}, ct);
            if (!validation.IsValid)
                return Result<bool>.FailureResult(validation.Errors[0].ErrorMessage, "VALIDATION_ERROR", HttpStatusCode.BadRequest);    

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var redisKey = $"OTP_{normalizedEmail}";
            var storedCodeValue = await _redis.GetAsync<string>(redisKey);
            if (string.IsNullOrEmpty(storedCodeValue))
            {
                return Result<bool>.FailureResult("OTP not found or expired.", "OTP_NOT_FOUND", HttpStatusCode.NotFound);
            }

            bool isValid = storedCodeValue.ToString() == code;
            if(isValid)
            {
                var User = await _redis.GetAsync<User>($"Email_{normalizedEmail}");
                await _authRepository.AddAsync(User);
                await _unitOfWork.SaveChangesAsync();

                return Result<bool>.SuccessResult(true, "OTP verified successfully.", HttpStatusCode.OK);
            }
            else
            {
                return Result<bool>.FailureResult("Invalid OTP code.", "OTP_INVALID", HttpStatusCode.BadRequest);
            }
        }
    }
}