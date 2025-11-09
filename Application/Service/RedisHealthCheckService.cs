using Application.Abstractions.Services;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Service
{
    public class RedisHealthCheckService : IRedisHealthCheckService
    {
        private readonly IConnectionMultiplexer _connection;

        public RedisHealthCheckService(IConnectionMultiplexer connection)
        {
            _connection = connection;
        }

        public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
        {
            try
            {
                if (_connection == null || !_connection.IsConnected)
                    return false;

                var db = _connection.GetDatabase();
                var pong = await db.PingAsync();
                return pong.TotalMilliseconds >= 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<long> GetLatencyAsync(CancellationToken ct = default)
        {
            try
            {
                var db = _connection.GetDatabase();
                var sw = Stopwatch.StartNew();
                await db.PingAsync();
                sw.Stop();
                return sw.ElapsedMilliseconds;
            }
            catch
            {
                return -1;
            }
        }
    }
}

