using Application.Abstractions.Common;
using Application.Abstractions.Repositories;
using Application.Abstractions.Repositories.Common;
using Domain.Entities.Identity;
using Infrastructure.Persistence.DatabaseContext;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Persistence.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ecom.Infrastructure.Persistence
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly ApplicationDbContext _context;

        // cache repository instances
        private readonly ConcurrentDictionary<Type, object> _repositories = new();

        // current EF Core transaction
        private IDbContextTransaction? _currentTransaction;

        // simple in-memory outbox queue (JSON strings). Replace with DB table for persistent outbox.
        private readonly ConcurrentQueue<string> _integrationOutbox = new();

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Convenience property for auth/user-specific repository.
        /// </summary>
        public IAuthRepository Users => (IAuthRepository)GetRepository(typeof(AuthRepository), typeof(User));

        /// <summary>
        /// Add integration event to an in-memory outbox (JSON). You can later persist/publish them from a background service.
        /// </summary>
        public Task AddIntegrationEventToOutboxAsync(object integrationEvent)
        {
            if (integrationEvent == null) throw new ArgumentNullException(nameof(integrationEvent));

            // Simple serialization - consider adding metadata (type name, occurredOn, retries, etc.)
            var payload = JsonSerializer.Serialize(new
            {
                Type = integrationEvent.GetType().FullName,
                Payload = integrationEvent,
                OccurredOn = DateTimeOffset.UtcNow
            });

            _integrationOutbox.Enqueue(payload);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Begin a new DB transaction. Nested begins will reuse the same transaction.
        /// </summary>
        public async Task BeginTransactionAsync()
        {
            if (_currentTransaction != null)
                return;

            _currentTransaction = await _context.Database.BeginTransactionAsync();
        }

        /// <summary>
        /// Commit current transaction (if any). Commits DB transaction and clears it.
        /// </summary>
        public async Task<bool> CommitTransactionAsync()
        {
            if (_currentTransaction == null)
                return false;

            try
            {
                await _context.SaveChangesAsync();
                await _currentTransaction.CommitAsync();
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
                return true;
            }
            catch
            {
                // in case of exception, attempt rollback
                try
                {
                    await _currentTransaction.RollbackAsync();
                    await _currentTransaction.DisposeAsync();
                }
                catch { /* swallow secondary errors */ }

                _currentTransaction = null;
                throw;
            }
        }

        /// <summary>
        /// Rollback current transaction if exists.
        /// </summary>
        public async Task<bool> RollbackTransactionAsync()
        {
            if (_currentTransaction == null)
                return false;

            try
            {
                await _currentTransaction.RollbackAsync();
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
                return true;
            }
            catch
            {
                _currentTransaction = null;
                throw;
            }
        }

        /// <summary>
        /// Save pending changes via DbContext.
        /// </summary>
        public Task<int> SaveChangesAsync()
        {
            return _context.SaveChangesAsync();
        }

        /// <summary>
        /// Dispose DbContext and transaction if still open.
        /// </summary>
        public void Dispose()
        {
            if (_currentTransaction != null)
            {
                try
                {
                    _currentTransaction.Rollback();
                }
                catch { /* ignore */ }

                try
                {
                    _currentTransaction.Dispose();
                }
                catch { /* ignore */ }

                _currentTransaction = null;
            }

            _context.Dispose();
            GC.SuppressFinalize(this);
        }

        // ---------- helper: generic repository factory ----------
        private object GetRepository(Type repoConcreteType, Type entityType)
        {
            // key by entity type (so AuthRepository and others cached)
            var key = entityType;

            if (_repositories.TryGetValue(key, out var existing))
                return existing;

            // create instance with ctor(ApplicationDbContext)
            var instance = Activator.CreateInstance(repoConcreteType, _context)
                ?? throw new InvalidOperationException($"Cannot create repository instance for {repoConcreteType.Name}");

            _repositories.TryAdd(key, instance);
            return instance;
        }

        // Optional generic access: returns Repository<T> for other entities
        public IRepository<T> Repository<T>() where T : class
        {
            var key = typeof(T);
            if (_repositories.TryGetValue(key, out var existing))
                return (IRepository<T>)existing;

            var repoType = typeof(Repository<>).MakeGenericType(typeof(T));
            var instance = Activator.CreateInstance(repoType, _context)
                ?? throw new InvalidOperationException($"Cannot create repository instance for {repoType.Name}");

            _repositories.TryAdd(key, instance);
            return (IRepository<T>)instance;
        }

        // Expose a way to flush/publish the in-memory outbox if needed.
        // This method is not part of IUnitOfWork but can be used by a background worker.
        public bool TryDequeueOutbox(out string serializedEvent)
        {
            return _integrationOutbox.TryDequeue(out serializedEvent!);
        }
    }
}
