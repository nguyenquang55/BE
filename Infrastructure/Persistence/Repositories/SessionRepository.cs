using Application.Abstractions.Repositories;
using Domain.Entities.Identity;
using Infrastructure.Persistence.DatabaseContext;
using Infrastructure.Persistence.Repositories.Common;
using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Repositories
{
    public class SessionRepository : Repository<Session>, ISessionRepository
    {
        private readonly ApplicationDbContext _context;

        public SessionRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task DisableSessionAsync(Guid userID,string reason ,CancellationToken ct = default)
        {
            if (userID == Guid.Empty)
                throw new ArgumentException("UserID không tồn tại", nameof(userID));

            var sessions = await _context.Set<Session>().Where(s => s.UserId == userID && !s.IsRevoked).ToListAsync(ct);
            if (sessions == null || sessions.Count == 0)
                return;

            foreach (var session in sessions)
            {
                session.IsRevoked = true;
                session.UpdatedAt = DateTime.Now;
                session.UpdatedBy = reason;
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task<Session?> GetSessionByToken(string Token, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(Token))
                throw new ArgumentException("Token không tồn tại", nameof(Token));

            var session = await _context.Set<Session>().Where(s => s.SessionToken == Token && s.IsRevoked == false).FirstOrDefaultAsync(ct);
            if (session == null)
                return null;

            //if ((session.ExpiresAt.HasValue && session.ExpiresAt.Value <= DateTime.UtcNow) || session.IsRevoked)
            //{
            //    return null;
            //}

            return session;
        }
    }
}
