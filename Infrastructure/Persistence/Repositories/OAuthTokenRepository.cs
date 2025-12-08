using Application.Abstractions.Repositories;
using Domain.Entities.Identity;
using Infrastructure.Persistence.DatabaseContext;
using Infrastructure.Persistence.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Repositories
{
    public class OAuthTokenRepository : Repository<OAuthToken>, IOAuthTokenRepository
    {
        public OAuthTokenRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<OAuthToken?> GetLatestByProviderAsync(Guid userId, Guid providerId, CancellationToken ct = default)
        {
            if (userId == Guid.Empty) throw new ArgumentException("userId is required", nameof(userId));
            if (providerId == Guid.Empty) throw new ArgumentException("providerId is required", nameof(providerId));

            return await _context.OAuthTokens
                .Where(t => t.UserId == userId && t.AuthProviderId == providerId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }
    }
}
