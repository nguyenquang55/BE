using Application.Abstractions.Common;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Contracts.Contact;
using Domain.Entities.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Service
{
    public class ContactService : IContactService
    {
        private readonly IContactRepository _contacts;
        private readonly IUnitOfWork _uow;
        private readonly IValidator<CreateContactRequest> _createValidator;
        private readonly IValidator<UpdateContactRequest> _updateValidator;
        private readonly ISessionService _sessions;

        public ContactService(IContactRepository contacts, IUnitOfWork uow, IValidator<CreateContactRequest> createValidator, IValidator<UpdateContactRequest> updateValidator, ISessionService sessions)
        {
            _contacts = contacts;
            _uow = uow;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _sessions = sessions;
        }

        private async Task<Guid?> ResolveUserIdAsync(string? sessionToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) return null;
            var res = await _sessions.GetSessionByTokenAsync(sessionToken, ct);
            return res.Success ? res.Data!.UserId : (Guid?)null;
        }

        public async Task<Result<ContactDTO>> CreateAsync(string? sessionToken, CreateContactRequest request, CancellationToken ct = default)
        {
            var userId = await ResolveUserIdAsync(sessionToken, ct);
            if (userId == null) return Result<ContactDTO>.FailureResult("Unauthorized", "UNAUTHORIZED", HttpStatusCode.Unauthorized);
            var v = await _createValidator.ValidateAsync(request, ct);
            if (!v.IsValid)
                return Result<ContactDTO>.FailureResult(v.Errors.First().ErrorMessage, "VALIDATION_ERROR", HttpStatusCode.BadRequest);

            var dup = await _contacts.FindByEmailAsync(userId.Value, request.Email, ct);
            if (dup != null)
                return Result<ContactDTO>.FailureResult("Contact email already exists", "CONFLICT", HttpStatusCode.Conflict);

            var entity = new Contact
            {
                UserId = userId.Value,
                Name = request.Name.Trim(),
                Email = request.Email.Trim(),
                Source = string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source!.Trim()
            };

            await _contacts.AddAsync(entity);
            await _uow.SaveChangesAsync();

            return Result<ContactDTO>.SuccessResult(ToDto(entity), "Created", HttpStatusCode.Created);
        }

        public async Task<Result<BulkCreateContactsResponse>> CreateManyAsync(string? sessionToken, IEnumerable<CreateContactRequest> requests, CancellationToken ct = default)
        {
            var userId = await ResolveUserIdAsync(sessionToken, ct);
            if (userId == null) return Result<BulkCreateContactsResponse>.FailureResult("Unauthorized", "UNAUTHORIZED", HttpStatusCode.Unauthorized);
            if (requests == null)
                return Result<BulkCreateContactsResponse>.FailureResult("Request body is null", "NULL_BODY", HttpStatusCode.BadRequest);

            var list = requests.ToList();
            if (list.Count == 0)
                return Result<BulkCreateContactsResponse>.FailureResult("Empty list", "EMPTY_LIST", HttpStatusCode.BadRequest);

            var resp = new BulkCreateContactsResponse { TotalRequested = list.Count };

            // Validate and normalize; deduplicate within batch (by email and by name)
            var normalizedEmailToIndex = new Dictionary<string, int>();
            var normalizedNameToIndex = new Dictionary<string, int>();
            var candidates = new List<(int Index, CreateContactRequest Req, string NormEmail)>();

            for (int i = 0; i < list.Count; i++)
            {
                var req = list[i];
                var v = await _createValidator.ValidateAsync(req, ct);
                if (!v.IsValid)
                {
                    resp.Errors.Add(new BulkItemError
                    {
                        Index = i,
                        Email = req?.Email ?? string.Empty,
                        Message = v.Errors.First().ErrorMessage,
                        Code = "VALIDATION_ERROR"
                    });
                    continue;
                }

                var norm = req.Email.Trim().ToLowerInvariant();
                var normName = req.Name.Trim().ToLowerInvariant();

                if (normalizedEmailToIndex.ContainsKey(norm))
                {
                    resp.Errors.Add(new BulkItemError
                    {
                        Index = i,
                        Email = req.Email,
                        Message = "Duplicate email in request list",
                        Code = "DUPLICATE_IN_BATCH"
                    });
                    continue;
                }

                if (normalizedNameToIndex.ContainsKey(normName))
                {
                    resp.Errors.Add(new BulkItemError
                    {
                        Index = i,
                        Email = req.Email,
                        Message = "Duplicate name in request list",
                        Code = "DUPLICATE_NAME_IN_BATCH"
                    });
                    continue;
                }

                normalizedEmailToIndex[norm] = i;
                normalizedNameToIndex[normName] = i;
                candidates.Add((i, req, norm));
            }

            if (candidates.Count == 0)
            {
                resp.CreatedCount = 0;
                resp.ErrorCount = resp.Errors.Count;
                return Result<BulkCreateContactsResponse>.SuccessResult(resp, "No valid items to create", HttpStatusCode.OK);
            }

            // Check existing emails and names in DB
            var existing = await _contacts.FindByEmailsAsync((Guid)userId, candidates.Select(c => c.NormEmail), ct);
            var existingSet = existing.Select(e => e.Email.ToLowerInvariant()).ToHashSet();
            var existingNames = await _contacts.FindByNamesAsync((Guid)userId, candidates.Select(c => c.Req.Name), ct);
            var existingNameSet = existingNames.Select(e => e.Name.ToLowerInvariant()).ToHashSet();

            var toCreate = new List<Contact>();
            foreach (var c in candidates)
            {
                if (existingSet.Contains(c.NormEmail))
                {
                    resp.Errors.Add(new BulkItemError
                    {
                        Index = c.Index,
                        Email = c.Req.Email,
                        Message = "Contact email already exists",
                        Code = "CONFLICT"
                    });
                    continue;
                }

                if (existingNameSet.Contains(c.Req.Name.Trim().ToLowerInvariant()))
                {
                    resp.Errors.Add(new BulkItemError
                    {
                        Index = c.Index,
                        Email = c.Req.Email,
                        Message = "Contact name already exists",
                        Code = "DUPLICATE_NAME"
                    });
                    continue;
                }

                toCreate.Add(new Contact
                {
                    UserId = (Guid)userId,
                    Name = c.Req.Name.Trim(),
                    Email = c.Req.Email.Trim(),
                    Source = string.IsNullOrWhiteSpace(c.Req.Source) ? "manual" : c.Req.Source!.Trim()
                });
            }

            if (toCreate.Count > 0)
            {
                await _contacts.AddRangeAsync(toCreate);
                await _uow.SaveChangesAsync();

                resp.Created.AddRange(toCreate.Select(ToDto));
            }

            resp.CreatedCount = resp.Created.Count;
            resp.ErrorCount = resp.Errors.Count;

            var message = resp.ErrorCount == 0 ? "Created all" : (resp.CreatedCount == 0 ? "No contacts created" : "Partially created");
            var status = resp.CreatedCount > 0 ? HttpStatusCode.Created : HttpStatusCode.BadRequest;
            return Result<BulkCreateContactsResponse>.SuccessResult(resp, message, status);
        }

        public async Task<Result<bool>> DeleteAsync(string? sessionToken, Guid contactId, CancellationToken ct = default)
        {
            var uid = await ResolveUserIdAsync(sessionToken, ct);
            if (uid == null) return Result<bool>.FailureResult("Unauthorized", "UNAUTHORIZED", HttpStatusCode.Unauthorized);
            var existing = await _contacts.GetByIdAsync((Guid)uid, contactId, ct);
            if (existing == null)
                return Result<bool>.FailureResult("Not found", "NOT_FOUND", HttpStatusCode.NotFound);

            _contacts.Remove(existing);
            await _uow.SaveChangesAsync();
            return Result<bool>.SuccessResult(true, "Deleted", HttpStatusCode.OK);
        }

        public async Task<Result<ContactDTO>> GetAsync(string? sessionToken, Guid contactId, CancellationToken ct = default)
        {
            var uid = await ResolveUserIdAsync(sessionToken, ct);
            if (uid == null) return Result<ContactDTO>.FailureResult("Unauthorized", "UNAUTHORIZED", HttpStatusCode.Unauthorized);
            var entity = await _contacts.GetByIdAsync(uid.Value, contactId, ct);
            if (entity == null)
                return Result<ContactDTO>.FailureResult("Not found", "NOT_FOUND", HttpStatusCode.NotFound);
            return Result<ContactDTO>.SuccessResult(ToDto(entity), "OK", HttpStatusCode.OK);
        }

        public async Task<Result<IEnumerable<ContactDTO>>> ListAsync(string? sessionToken, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var uid = await ResolveUserIdAsync(sessionToken, ct);
            if (uid == null) return Result<IEnumerable<ContactDTO>>.FailureResult("Unauthorized", "UNAUTHORIZED", HttpStatusCode.Unauthorized);
            var items = await _contacts.ListAsync(uid.Value, search, page, pageSize, ct);
            return Result<IEnumerable<ContactDTO>>.SuccessResult(items.Select(ToDto), "OK", HttpStatusCode.OK);
        }

        public async Task<Result<ContactDTO>> UpdateAsync(string? sessionToken, Guid contactId, UpdateContactRequest request, CancellationToken ct = default)
        {
            var userId = await ResolveUserIdAsync(sessionToken, ct);
            if (userId == null) return Result<ContactDTO>.FailureResult("Unauthorized", "UNAUTHORIZED", HttpStatusCode.Unauthorized);
            var v = await _updateValidator.ValidateAsync(request, ct);
            if (!v.IsValid)
                return Result<ContactDTO>.FailureResult(v.Errors.First().ErrorMessage, "VALIDATION_ERROR", HttpStatusCode.BadRequest);

            var entity = await _contacts.GetByIdAsync((Guid)userId, contactId, ct);
            if (entity == null)
                return Result<ContactDTO>.FailureResult("Not found", "NOT_FOUND", HttpStatusCode.NotFound);

            if (!string.Equals(entity.Email, request.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var dup = await _contacts.FindByEmailAsync((Guid)userId, request.Email!, ct);
                if (dup != null)
                    return Result<ContactDTO>.FailureResult("Contact email already exists", "CONFLICT", HttpStatusCode.Conflict);
            }

            entity.Name = request.Name.Trim();
            entity.Email = request.Email.Trim();
            entity.Source = string.IsNullOrWhiteSpace(request.Source) ? entity.Source : request.Source!.Trim();

            _contacts.Update(entity);
            await _uow.SaveChangesAsync();

            return Result<ContactDTO>.SuccessResult(ToDto(entity), "Updated", HttpStatusCode.OK);
        }

        private static ContactDTO ToDto(Contact e) => new ContactDTO
        {
            Id = e.Id,
            UserId = e.UserId,
            Name = e.Name,
            Email = e.Email,
            Source = e.Source
        };
    }
}
