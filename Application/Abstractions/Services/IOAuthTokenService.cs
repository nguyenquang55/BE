using Microsoft.AspNetCore.Http.HttpResults;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface IOAuthTokenService
    {
        Task<string> RefreshAccessToken(Guid userId);
        Task<string> GetAccessToken(Guid userId);
    }
}
