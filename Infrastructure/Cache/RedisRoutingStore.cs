using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Application.Abstractions.Infrastructure;

namespace Infrastructure.Cache
{
    public class RedisRoutingStore : IRoutingStore
    {
        private readonly IRedisCacheService _cache;
        private readonly TimeSpan _defaultTtl;

        public RedisRoutingStore(IRedisCacheService cache, IConfiguration cfg)
        {
            _cache = cache;
            var ttlSec = cfg.GetValue<int>("Routing:DefaultTTLSeconds", 600);
            _defaultTtl = TimeSpan.FromSeconds(ttlSec > 0 ? ttlSec : 600);
        }

        private static string Key(string messageId) => $"route:{messageId}";

        public async Task SaveAsync(string messageId, MessageRoute route, TimeSpan ttl, CancellationToken ct = default)
        {
            await _cache.SetAsync(Key(messageId), route, ttl == default ? _defaultTtl : ttl);
        }

        public async Task<MessageRoute?> TryGetAsync(string messageId, CancellationToken ct = default)
        {
            return await _cache.GetAsync<MessageRoute>(Key(messageId));
        }

        public async Task RemoveAsync(string messageId, CancellationToken ct = default)
        {
            await _cache.RemoveAsync(Key(messageId));
        }
    }
}
