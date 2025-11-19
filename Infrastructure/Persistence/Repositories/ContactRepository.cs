using Application.Abstractions.Repositories;
using Domain.Entities.Identity;
using Infrastructure.Persistence.DatabaseContext;
using Infrastructure.Persistence.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Repositories
{
    public class ContactRepository : Repository<Contact>, IContactRepository
    {
        public ContactRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Contact?> GetByIdAsync(Guid userId, Guid contactId, CancellationToken ct = default)
        {
            return await _context.Set<Contact>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == contactId && c.UserId == userId, ct);
        }

        public async Task<Contact?> FindByEmailAsync(Guid userId, string email, CancellationToken ct = default)
        {
            var normalized = email.Trim().ToLowerInvariant();
            return await _context.Set<Contact>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Email.ToLower() == normalized, ct);
        }

        public async Task<IReadOnlyList<Contact>> FindByEmailsAsync(Guid userId, IEnumerable<string> emails, CancellationToken ct = default)
        {
            var lower = emails.Where(e => !string.IsNullOrWhiteSpace(e))
                              .Select(e => e.Trim().ToLowerInvariant())
                              .Distinct()
                              .ToList();
            if (lower.Count == 0) return Array.Empty<Contact>();

            return await _context.Set<Contact>()
                .AsNoTracking()
                .Where(c => c.UserId == userId && lower.Contains(c.Email.ToLower()))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Contact>> FindByNamesAsync(Guid userId, IEnumerable<string> names, CancellationToken ct = default)
        {
            var lower = names.Where(n => !string.IsNullOrWhiteSpace(n))
                             .Select(n => n.Trim().ToLowerInvariant())
                             .Distinct()
                             .ToList();
            if (lower.Count == 0) return Array.Empty<Contact>();

            return await _context.Set<Contact>()
                .AsNoTracking()
                .Where(c => c.UserId == userId && lower.Contains(c.Name.ToLower()))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Contact>> ListAsync(Guid userId, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 200) pageSize = 20;

            var query = _context.Set<Contact>().AsNoTracking().Where(c => c.UserId == userId);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(c => c.Name.ToLower().Contains(s) || c.Email.ToLower().Contains(s));
            }

            return await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Contact>> ListAllAsync(Guid userId, CancellationToken ct = default)
        {
            return await _context.Set<Contact>()
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Name)
                .ToListAsync(ct);
        }
    }
}
