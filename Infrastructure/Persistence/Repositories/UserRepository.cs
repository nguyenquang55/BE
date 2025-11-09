using Application.Abstractions.Repositories;
using Domain.Entities.Identity;
using Infrastructure.Persistence.DatabaseContext;
using Infrastructure.Persistence.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<User?> GetAllByIdAsync(string id, CancellationToken ct = default)
        {
            if (!Guid.TryParse(id, out var guidId))
                return null;

            return await _context.Users
                .Include(u => u.AuthProviders)
                .Include(u => u.OAuthTokens)
                .Include(u => u.Calendars)
                .Include(u => u.EmailThreads)
                .Include(u => u.Requests)
                .FirstOrDefaultAsync(u => u.Id == guidId, ct);
        }

        public async Task<User?> GetWithAuthProvidersAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.Users
                .Include(u => u.AuthProviders)
                .FirstOrDefaultAsync(u => u.Id == id, ct);
        }
    }
}
