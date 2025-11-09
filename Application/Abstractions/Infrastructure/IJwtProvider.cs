using Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Infrastructure
{
    public interface IJwtProvider
    {
        Task<string> GenerateAccessToken(User user);
        Task<string> GenerateRefreshToken(User user);
        Task<string> GenerateSessionToken (User user);
        Task<(string AccessToken, string RefreshToken)> GenerateToken(User user);
    }
}
