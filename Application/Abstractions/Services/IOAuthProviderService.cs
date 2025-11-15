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
        Task<Result<string>> CreateAuthorizationUrlAsync(string sessionToken);
        Task<Result<string>> HandleCallbackAsync(string code);
        Task<Result<string>> HandleCallbackAsync(string code, string state);
    }
}
