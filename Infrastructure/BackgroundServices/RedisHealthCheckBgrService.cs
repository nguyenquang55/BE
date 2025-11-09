using Application.Abstractions.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.BackgroundServices
{
    public class RedisHealthCheckBgrService : BackgroundService
    {
        private readonly IRedisHealthCheckService _health;
        private readonly ILogger<RedisHealthCheckBgrService> _logger;

        public RedisHealthCheckBgrService(IRedisHealthCheckService health, ILogger<RedisHealthCheckBgrService> logger)
        {
            _health = health;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Redis background monitor started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                var healthy = await _health.IsConnectedAsync(stoppingToken);
                if (!healthy)
                {
                    DateTime Time = DateTime.Now;
                    _logger.LogWarning($"Redis disconnected at {Time}!");
                }
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
