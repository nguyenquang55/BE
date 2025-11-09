using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface IRedisHealthCheckService
    {
        /// <summary>
        /// Ping Redis server and return whether connection is healthy.
        /// </summary>
        Task<bool> IsConnectedAsync(CancellationToken ct = default);

        /// <summary>
        /// Get the latency (ping time) in milliseconds.
        /// </summary>
        Task<long> GetLatencyAsync(CancellationToken ct = default);
    }
}
