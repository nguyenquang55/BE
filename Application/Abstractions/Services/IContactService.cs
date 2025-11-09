using Application.Contracts.Contact;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface IContactService
    {
        Task<Result<ContactDTO>> CreateAsync(Guid userId, CreateContactRequest request, CancellationToken ct = default);
        Task<Result<BulkCreateContactsResponse>> CreateManyAsync(Guid userId, IEnumerable<CreateContactRequest> requests, CancellationToken ct = default);
        Task<Result<ContactDTO>> GetAsync(Guid userId, Guid contactId, CancellationToken ct = default);
        Task<Result<IEnumerable<ContactDTO>>> ListAsync(Guid userId, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
        Task<Result<ContactDTO>> UpdateAsync(Guid userId, Guid contactId, UpdateContactRequest request, CancellationToken ct = default);
        Task<Result<bool>> DeleteAsync(Guid userId, Guid contactId, CancellationToken ct = default);
    }
}
