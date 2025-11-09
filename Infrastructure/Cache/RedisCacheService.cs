using Application.Abstractions.Infrastructure;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.Cache
{
    public class RedisCacheService : IRedisCacheService
    {
        private readonly IConnectionMultiplexer _mux;
        private readonly IDatabase _db;

        public RedisCacheService(IConnectionMultiplexer mux)
        {
            _mux = mux ?? throw new ArgumentNullException(nameof(mux));
            _db = _mux.GetDatabase();
        }

        #region String Cache (serialize object)
        /// <summary>
        /// Lấy object từ Redis theo key (được lưu ở dạng JSON string).
        /// </summary>
        public async Task<T?> GetAsync<T>(string key)
        {
            var value = await _db.StringGetAsync(key);
            return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
        }

        /// <summary>
        /// Lưu object vào Redis theo key (serialize sang JSON string).
        /// </summary>
        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var serialized = JsonSerializer.Serialize(value);
            await _db.StringSetAsync(key, serialized, expiry);
        }
        #endregion

        #region Hash Cache (field-value)
        /// <summary>
        /// Lưu nhiều field-value vào Redis Hash theo key.
        /// </summary>
        public async Task HashSetAsync(string key, Dictionary<string, string> values, TimeSpan? expiry = null)
        {
            var entries = values.Select(x => new HashEntry(x.Key, x.Value)).ToArray();
            await _db.HashSetAsync(key, entries);

            if (expiry.HasValue)
                await _db.KeyExpireAsync(key, expiry);
        }

        /// <summary>
        /// Lưu field-value vào Redis Hash theo key.
        /// </summary>
        public async Task HashSetAsync(string key, string field, string value, TimeSpan? expiry = null)
        {
            await _db.HashSetAsync(key, field, value);
            if (expiry.HasValue)
                await _db.KeyExpireAsync(key, expiry);
        }

        /// <summary>
        /// Lấy giá trị của 1 field trong Redis Hash.
        /// </summary>
        public async Task<string?> HashGetAsync(string key, string field)
        {
            var value = await _db.HashGetAsync(key, field);
            return value.HasValue ? value.ToString() : null;
        }

        /// <summary>
        /// Lấy toàn bộ field-value trong Redis Hash theo key.
        /// </summary>
        public async Task<Dictionary<string, string>?> HashGetAllAsync(string key)
        {
            var entries = await _db.HashGetAllAsync(key);
            if (entries.Length == 0)
                return null;

            return entries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        }

        /// <summary>
        /// Xoá 1 field trong Redis Hash theo key.
        /// </summary>
        public async Task HashRemoveAsync(string key, string field)
        {
            await _db.HashDeleteAsync(key, field);
        }
        #endregion

        #region Common Remove
        /// <summary>
        /// Xoá 1 key trong Redis (dạng String hoặc Hash).
        /// </summary>
        public async Task RemoveAsync(string key) => await _db.KeyDeleteAsync(key);

        /// <summary>
        /// Xoá nhiều key trong Redis theo pattern.
        /// Lưu ý: Keys(pattern) chỉ chạy được khi Redis bật `allow-admin`.
        /// </summary>
        public async Task RemoveByPatternAsync(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Pattern must not be null or empty.", nameof(pattern));

            var endpoints = _mux.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _mux.GetServer(endpoint);
                if (!server.IsConnected)
                    continue;

                var keys = server.Keys(pattern: pattern + "*").ToArray();

                if (keys.Length == 0)
                    continue;

                foreach (var key in keys)
                {
                    await _db.KeyDeleteAsync(key);
                }
            }
        }
        #endregion
    }
}