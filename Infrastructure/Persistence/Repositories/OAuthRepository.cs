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
    public class OAuthRepository : Repository<OAuthToken>, IOAuthRepository
    {
        public OAuthRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<string> GetOAuthTokenAsync(string useId, CancellationToken ct = default)
        {
            var token = await _context.OAuthTokens
                .Where(t => t.UserId.ToString() == useId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => t.RefreshToken)
                .FirstOrDefaultAsync(ct);
            return token ?? string.Empty;
        }
    }
}
