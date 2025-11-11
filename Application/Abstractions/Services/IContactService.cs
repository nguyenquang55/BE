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
        Task<Result<ContactDTO>> CreateAsync(string? sessionToken, CreateContactRequest request, CancellationToken ct = default);
        Task<Result<BulkCreateContactsResponse>> CreateManyAsync(string? sessionToken, IEnumerable<CreateContactRequest> requests, CancellationToken ct = default);
        Task<Result<ContactDTO>> GetAsync(string? sessionToken, Guid contactId, CancellationToken ct = default);
        Task<Result<IEnumerable<ContactDTO>>> ListAsync(string? sessionToken, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
        Task<Result<ContactDTO>> UpdateAsync(string? sessionToken, Guid contactId, UpdateContactRequest request, CancellationToken ct = default);
        Task<Result<bool>> DeleteAsync(string? sessionToken, Guid contactId, CancellationToken ct = default);
    }
}
