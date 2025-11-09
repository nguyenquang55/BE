using Microsoft.AspNetCore.Http.HttpResults;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface IValidateEmailService
    {
        Task<Result<string>> SendOtpAsync(string email, string purpose = "Register", CancellationToken ct = default);
        Task<Result<bool>> VerifyOtpAsync(string email, string code, string purpose = "Register", CancellationToken ct = default);
    }
}
