using Application.Abstractions.Repositories;
using Application.Abstractions.Repositories.Common;
using Domain.Entities.Identity;
using Infrastructure.Persistence.DatabaseContext;
using Infrastructure.Persistence.Repositories.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    class OAuthProviderRepository : Repository<OAuthProvider>, IOAuthProviderRepository
    {
        public OAuthProviderRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<bool> IsLinkedForUserAsync(Guid userId, string provider, string? providerEmail, string? providerUserId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("provider is required", nameof(provider));

            return await _context.OAuthProviders
                .AsNoTracking()
                .AnyAsync(p => p.UserId == userId
                               && p.Provider == provider
                               && (
                                   (!string.IsNullOrEmpty(providerEmail) && p.ProviderEmail == providerEmail)
                                   || (!string.IsNullOrEmpty(providerUserId) && p.ProviderUserId == providerUserId)
                               ), ct);
        }

        public async Task<bool> IsEmailLinkedByOtherAsync(Guid currentUserId, string provider, string providerEmail, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("provider is required", nameof(provider));
            if (string.IsNullOrWhiteSpace(providerEmail)) throw new ArgumentException("providerEmail is required", nameof(providerEmail));

            return await _context.OAuthProviders
                .AsNoTracking()
                .AnyAsync(p => p.Provider == provider
                               && p.ProviderEmail == providerEmail
                               && p.UserId != currentUserId, ct);
        }
    }
}
