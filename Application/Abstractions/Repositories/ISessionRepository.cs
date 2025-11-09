using Application.Abstractions.Repositories.Common;
using Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Repositories
{
    public interface ISessionRepository:IRepository<Session>
    {
        Task<Session?> GetSessionByToken(string Token, CancellationToken ct = default);
        Task DisableSessionAsync(Guid userID,string reason, CancellationToken ct = default);
    }
}
