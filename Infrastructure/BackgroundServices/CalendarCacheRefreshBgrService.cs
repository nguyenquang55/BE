using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Application.Abstractions.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;

namespace Infrastructure.BackgroundServices
{
    public class CalendarCacheRefreshBgrService : BackgroundService
    {
        private readonly ILogger<CalendarCacheRefreshBgrService> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly IRedisCacheService _redisCacheService;

        private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(5);

        public CalendarCacheRefreshBgrService(
            IRedisCacheService redisCacheService,
            ILogger<CalendarCacheRefreshBgrService> logger,
            IConnectionMultiplexer redis)
        {
            _redisCacheService = redisCacheService;
            _logger = logger;
            _redis = redis;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Starting Calendar Sync Cycle...");
                try
                {
                    var now = DateTime.Now;
                    string pattern = "user:*:acct:*:events:*";
                    var processedUsers = new HashSet<string>();

                    var server = _redis.GetServer(_redis.GetEndPoints().First());
                    var tasks = new List<Task>();

                    await foreach (var key in server.KeysAsync(pattern: pattern, pageSize: 500))
                    {
                        var (userId, accountEmail) = ParseKey(key);
                        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(accountEmail)) continue;

                        var userUniqueKey = $"{userId}:{accountEmail}";

                        if (!processedUsers.Contains(userUniqueKey))
                        {
                            processedUsers.Add(userUniqueKey);

                            tasks.Add(ProcessUserSyncAsync(userId!, accountEmail!, now, stoppingToken));
                        }
                    }

                    await Task.WhenAll(tasks);

                    _logger.LogInformation($"Calendar Sync Cycle Finished. Synced {processedUsers.Count} users.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Global Calendar cache refresh loop error");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task ProcessUserSyncAsync(string userId, string accountEmail, DateTime now, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                var accessToken = await _redisCacheService.GetAsync<string>($"OAuthAccessToken:{userId}");
                if (string.IsNullOrWhiteSpace(accessToken)) return;

                using var googleService = new CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken(accessToken),
                    ApplicationName = "CalendarApp"
                });

                var startUserTime = now.Date;
                var endUserTime = startUserTime.AddDays(7).AddTicks(-1);

                var collectedEvents = await FetchGoogleEventsAsync(googleService, startUserTime, endUserTime, token);

                await SaveEventsToRedisAsync(userId, accountEmail, collectedEvents, now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing calendar for user {UserId} - {Email}", userId, accountEmail);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<List<CacheEventDto>> FetchGoogleEventsAsync(CalendarService service, DateTime start, DateTime end, CancellationToken token)
        {
            var collected = new List<CacheEventDto>();
            string? pageToken = null;
            var startUtc = start.ToUniversalTime();
            var endUtc = end.ToUniversalTime();

            try
            {
                do
                {
                    var request = service.Events.List("primary");
                    request.ShowDeleted = false;
                    request.SingleEvents = true; 
                    request.MaxResults = 250;
                    request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
                    request.TimeMinDateTimeOffset = new DateTimeOffset(startUtc);
                    request.TimeMaxDateTimeOffset = new DateTimeOffset(endUtc);
                    request.Fields = "items(id,summary,start,end),nextPageToken";
                    request.PageToken = pageToken;

                    var events = await request.ExecuteAsync(token);
                    if (events.Items != null)
                    {
                        foreach (var item in events.Items)
                        {
                            var startOffset = item.Start.DateTimeDateTimeOffset ?? DateTimeOffset.Parse(item.Start.Date);
                            var endOffset = item.End.DateTimeDateTimeOffset ?? DateTimeOffset.Parse(item.End.Date);

                            collected.Add(new CacheEventDto
                            {
                                Id = item.Id,
                                Title = item.Summary ?? "(No Title)",
                                StartTime = startOffset.LocalDateTime,
                                EndTime = endOffset.LocalDateTime
                            });
                        }
                    }
                    pageToken = events.NextPageToken;
                }
                while (!string.IsNullOrEmpty(pageToken));
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Token expired for user during sync.");
            }

            return collected;
        }

        private async Task SaveEventsToRedisAsync(string userId, string accountEmail, List<CacheEventDto> events, DateTime now)
        {
            var db = _redis.GetDatabase();

            var groups = events.GroupBy(e => e.StartTime.Date);

            foreach (var grp in groups)
            {
                var day = grp.Key;
                var yyyyMMdd = day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                var dayKey = (RedisKey)$"user:{userId}:acct:{accountEmail}:events:{yyyyMMdd}";

                var activeEvents = grp.Where(x => x.EndTime >= now).ToList();

                if (activeEvents.Any())
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(activeEvents);

                    await db.StringSetAsync(dayKey, json, TimeSpan.FromDays(2));
                }
                else
                {
                    await db.KeyDeleteAsync(dayKey);
                }
            }
        }

        private (string? userId, string? email) ParseKey(RedisKey key)
        {
            var parts = key.ToString().Split(':');
            if (parts.Length >= 6)
            {
                return (parts[1], parts[3]);
            }
            return (null, null);
        }

        private sealed class CacheEventDto
        {
            public string? Id { get; set; }
            public string? Title { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
        }
    }
}