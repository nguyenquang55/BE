using Domain.Entities.Identity;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface IOAuthProviderService
    {
        //Task<Result<string>> ValidateClientAsync(string SessionToken);
        Task<Result<string>> CreateAuthorizationUrlAsync(string sessionToken);
        // Backward-compatible overload (some callers may still use 1-arg)
        Task<Result<string>> HandleCallbackAsync(string code);
        // Preferred overload with state
        Task<Result<string>> HandleCallbackAsync(string code, string state);
        Task<Result<string>> Refresh(string sessionToken, CancellationToken ct = default);
    }
}
