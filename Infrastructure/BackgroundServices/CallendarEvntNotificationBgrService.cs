using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Application.Abstractions.SignalR;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.BackgroundServices
{
    public class CallendarEvntNotificationBgrService : BackgroundService
    {
        private readonly ILogger<CallendarEvntNotificationBgrService> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly INotificationHubContext _hub;

        // configurable scan interval seconds
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(20);

        public CallendarEvntNotificationBgrService(
            ILogger<CallendarEvntNotificationBgrService> logger,
            IConnectionMultiplexer redis,
            INotificationHubContext hub)
        {
            _logger = logger;
            _redis = redis;
            _hub = hub;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var db = _redis.GetDatabase();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Strategy: list reminder index keys by pattern and process due items
                    foreach (var server in _redis.GetEndPoints())
                    {
                        var srv = _redis.GetServer(server);
                        if (!srv.IsConnected) continue;

                        // keys: remind:{userId}
                        var keys = srv.Keys(pattern: "remind:*");
                        foreach (var key in keys)
                        {
                            await ProcessRemindersForKey(db, key, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error scanning calendar reminders");
                }

                try { await Task.Delay(_interval, stoppingToken); } catch { }
            }
        }

        private async Task ProcessRemindersForKey(IDatabase db, RedisKey key, CancellationToken ct)
        {
            // ZSET of reminders: member=reminderId, score=notifyAtUtcEpoch
            var nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // get due members up to now
            var members = await db.SortedSetRangeByScoreAsync(key, double.NegativeInfinity, nowEpoch);
            if (members == null || members.Length == 0) return;

            foreach (var member in members)
            {
                var reminderId = member.ToString();
                var itemKey = (RedisKey)$"remind:item:{reminderId}";

                // distributed lock to avoid duplicates in multi-instance
                var lockKey = (RedisKey)$"lock:remind:{reminderId}";
                var acquired = await db.StringSetAsync(lockKey, "1", TimeSpan.FromSeconds(10), when: When.NotExists);
                if (!acquired) { continue; }

                try
                {
                    var fields = await db.HashGetAllAsync(itemKey);
                    if (fields == null || fields.Length == 0)
                    {
                        // remove from index if item missing
                        await db.SortedSetRemoveAsync(key, member);
                        continue;
                    }

                    var dict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var fe in fields)
                    {
                        dict[fe.Name.ToString()] = fe.Value.ToString();
                    }

                    if (dict.TryGetValue("processed", out var processed) && string.Equals(processed, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        await db.SortedSetRemoveAsync(key, member);
                        continue;
                    }

                    var userId = dict.TryGetValue("userId", out var u) ? u : null;
                    var title = dict.TryGetValue("title", out var t) ? t : "Sự kiện";
                    var startUtc = dict.TryGetValue("startUtc", out var s) ? s : null;
                    var offsetStr = dict.TryGetValue("offsetMinutes", out var o) ? o : "0";
                    _ = int.TryParse(offsetStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset);

                    // message content (VN)
                    var localStart = startUtc != null ? DateTimeOffset.Parse(startUtc).ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "";
                    var message = $"Nhắc lịch: '{title}' bắt đầu lúc {localStart}. Bạn được nhắc trước {offset} phút.";

                    if (!string.IsNullOrEmpty(userId))
                    {
                        await _hub.SendToUserAsync(userId, message);
                    }

                    // mark processed and remove from index
                    await db.HashSetAsync(itemKey, new HashEntry[]
                    {
                        new HashEntry("processed", "true")
                    });
                    await db.SortedSetRemoveAsync(key, member);

                    // set TTL for item to auto-expire
                    await db.KeyExpireAsync(itemKey, TimeSpan.FromHours(24));
                }
                catch (Exception ex)
                {
                    // in case of error, keep the item; it will be retried next scan
                    _logger?.LogError(ex, "Error processing reminder {ReminderId}", reminderId);
                }
                finally
                {
                    await db.KeyDeleteAsync(lockKey);
                }
            }
        }
    }
}
