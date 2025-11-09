using Application.Contracts.Session;
using Domain.Entities.Identity;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface ISessionService
    {
        Task<Result<SessionDTO>>CreateSessionAsync(Guid userId,CancellationToken ct = default);
        Task<Result<SessionDTO?>> GetSessionByTokenAsync(string sessionToken, CancellationToken ct = default);
        Task<Result<SessionDTO?>> RefreshSessionAsync(string sessionToken, CancellationToken ct = default);
        Task<bool> RevokeSessionAsync(string sessionToken, CancellationToken ct = default);
        Task<IEnumerable<SessionDTO>> ListSessionsForUserAsync(Guid userId, CancellationToken ct = default);
        Task<bool> IsSessionValidAsync(string sessionToken, CancellationToken ct = default);
    }
}
