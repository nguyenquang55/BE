using Application.Abstractions.Repositories.Common;
using Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Repositories
{
    public interface IOAuthProviderRepository : IRepository<OAuthProvider>
    {
        Task<bool> IsLinkedForUserAsync(Guid userId, string provider, string? providerEmail, string? providerUserId, CancellationToken ct = default);
        Task<bool> IsEmailLinkedByOtherAsync(Guid currentUserId, string provider, string providerEmail, CancellationToken ct = default);
    }
}
