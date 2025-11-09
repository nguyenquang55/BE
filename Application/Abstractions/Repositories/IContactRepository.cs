using Application.Abstractions.Repositories.Common;
using Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstractions.Repositories
{
    public interface IContactRepository : IRepository<Contact>
    {
        Task<Contact?> GetByIdAsync(Guid userId, Guid contactId, CancellationToken ct = default);
        Task<Contact?> FindByEmailAsync(Guid userId, string email, CancellationToken ct = default);
        Task<IReadOnlyList<Contact>> FindByEmailsAsync(Guid userId, IEnumerable<string> emails, CancellationToken ct = default);
        Task<IReadOnlyList<Contact>> FindByNamesAsync(Guid userId, IEnumerable<string> names, CancellationToken ct = default);
        Task<IReadOnlyList<Contact>> ListAsync(Guid userId, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    }
}
