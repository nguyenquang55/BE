using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Application.Abstractions.SignalR;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Infrastructure;
using System.Linq;

namespace Infrastructure.BackgroundServices
{
    public class CallendarEvntNotificationBgrService : BackgroundService
    {
        private readonly ILogger<CallendarEvntNotificationBgrService> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly IRedisCacheService _redisCacheService;
        private readonly INotificationHubContext _hub;

        private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

        public CallendarEvntNotificationBgrService(
            IRedisCacheService redisCacheService,
            ILogger<CallendarEvntNotificationBgrService> logger,
            IConnectionMultiplexer redis,
            INotificationHubContext hub)
        {
            _redisCacheService = redisCacheService;
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
                    var now = DateTime.Now;
                    string todayStr = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                    string dayPattern = $"user:*:acct:*:events:{todayStr}";

                    foreach (var ep in _redis.GetEndPoints())
                    {
                        var server = _redis.GetServer(ep);
                        await foreach (var key in server.KeysAsync(pattern: dayPattern, pageSize: 1000, flags: CommandFlags.None))
                        {
                            try
                            {
                                var type = await db.KeyTypeAsync(key);

                                // Extract userId and accountEmail from key
                                string? userId = null;
                                string? accountEmail = null;
                                var parts = key.ToString().Split(':');
                                if (parts.Length >= 6)
                                {
                                    userId = parts[1];
                                    accountEmail = parts[3];
                                }

                                if (type == RedisType.String)
                                {
                                    var raw = await db.StringGetAsync(key);
                                    if (raw.IsNullOrEmpty) continue;

                                    var items = System.Text.Json.JsonSerializer.Deserialize<List<SearchEventResponeDto>>(
                                                    raw!, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                                                ?? new List<SearchEventResponeDto>();

                                    foreach (var it in items)
                                    {
                                        
                                        var minutesToStart = (it.StartTime - now).TotalMinutes;
                                        if (minutesToStart >= 0 && minutesToStart <= 30)
                                        {
                                            var unique = !string.IsNullOrWhiteSpace(it.Id) ? it.Id! : it.StartTime.Ticks.ToString();
                                            var dedupeKey = (RedisKey)$"lock:eventnotify:{key}:{unique}";
                                            var acquired = await db.StringSetAsync(dedupeKey, "1", TimeSpan.FromSeconds(15), when: When.NotExists);
                                            if (!acquired) continue;

                                            var accountLabel = string.IsNullOrWhiteSpace(accountEmail) ? string.Empty : $" [{accountEmail}]";
                                            var msg = new
                                            {
                                                type = "CalendarEventReminder",
                                                eventTime = it.StartTime,
                                                message = $"Sắp đến giờ{accountLabel}: '{it.Title ?? "(No Title)"}' bắt đầu lúc {it.StartTime:dd/MM/yyyy HH:mm}."
                                            };
                                            var payloadJson = System.Text.Json.JsonSerializer.Serialize(msg);
                                            if (!string.IsNullOrWhiteSpace(userId))
                                            {
                                                var connectionId = await _redisCacheService.GetAsync<string>($"connectionId:{userId}");
                                                if (!string.IsNullOrWhiteSpace(connectionId))
                                                    await _hub.SendToClientAsync(connectionId, payloadJson);
                                            }
                                        }
                                    }
                                }
                                else if (type == RedisType.Hash)
                                {
                                    // Legacy/alternative format stored as hash
                                    var hashFields = await db.HashGetAllAsync(key);
                                    foreach (var field in hashFields)
                                    {
                                        if (DateTime.TryParse(field.Name, out DateTime eventTime))
                                        {
                                            var minutesToStart = (eventTime - now).TotalMinutes;
                                            if (minutesToStart >= 0 && minutesToStart <= 15)
                                            {
                                                var unique = eventTime.Ticks.ToString();
                                                var dedupeKey = (RedisKey)$"lock:eventnotify:{key}:{unique}";
                                                var acquired = await db.StringSetAsync(dedupeKey, "1", TimeSpan.FromMinutes(10), when: When.NotExists);
                                                if (!acquired) continue;

                                                var title = field.Value.HasValue ? field.Value.ToString() : "Sự kiện";
                                                var accountLabel = string.IsNullOrWhiteSpace(accountEmail) ? string.Empty : $" [{accountEmail}]";
                                                var msg = new
                                                {
                                                    type = "CalendarEventReminder",
                                                    eventTime = eventTime,
                                                    message = $"Sắp đến giờ{accountLabel}: '{title}' bắt đầu lúc {eventTime:dd/MM/yyyy HH:mm}."
                                                };
                                                var payloadJson = System.Text.Json.JsonSerializer.Serialize(msg);
                                                if (!string.IsNullOrWhiteSpace(userId))
                                                {
                                                    var connectionId = await _redisCacheService.GetAsync<string>($"connectionId:{userId}");
                                                    if (!string.IsNullOrWhiteSpace(connectionId))
                                                        await _hub.SendToClientAsync(connectionId, payloadJson);
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // Skip other types to avoid WRONGTYPE
                                    continue;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing calendar event notifications for key {Key}", key);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Calendar notification loop error");
                }

                try { await Task.Delay(_interval, stoppingToken); } catch { }
            }
        }

        

        private sealed class SearchEventResponeDto
        {
            public string? Id { get; set; }
            public string? Title { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
        }
    }
}
