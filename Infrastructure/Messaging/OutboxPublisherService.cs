using Domain.Entities;
using Domain.Entities.Identity;
using Ecom.Infrastructure.Persistence;
using Infrastructure.Persistence.DatabaseContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace Infrastructure.Outbox
{
    /// <summary>
    /// Dịch vụ nền (BackgroundService) để publish các message từ Outbox ra RabbitMQ qua MassTransit.
    /// Đọc và xử lý các OutboxMessage chưa được publish, deserialize và gửi qua IPublishEndpoint.
    /// Hỗ trợ batch processing, parallel execution và retry logic.
    /// </summary>
    public class OutboxPublisherService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<OutboxPublisherService> _logger;
        private readonly IConfiguration _cfg;
        private readonly int _pollSeconds;
        private readonly int _batchSize;
        private readonly int _parallel;

        public OutboxPublisherService(IServiceProvider sp, ILogger<OutboxPublisherService> logger, IConfiguration cfg)
        {
            _sp = sp; _logger = logger; _cfg = cfg;
            _pollSeconds = _cfg.GetValue<int>("Outbox:PollIntervalSeconds", 5);
            _batchSize = _cfg.GetValue<int>("Outbox:BatchSize", 50);
            _parallel = _cfg.GetValue<int>("Outbox:MaxDegreeOfParallelism", 4);
        }

        /// <summary>
        /// Thực thi vòng lặp chính để poll và publish OutboxMessage.
        /// Sử dụng Dataflow để xử lý song song, cập nhật trạng thái Processed sau khi publish thành công.
        /// Xử lý lỗi bằng cách tăng RetryCount và log.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxPublisherService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                    var msgs = await db.OutboxMessages.Where(m => !m.Processed).OrderBy(m => m.OccurredOn).Take(_batchSize).ToListAsync(stoppingToken);
                    if (msgs.Count == 0) { await Task.Delay(TimeSpan.FromSeconds(_pollSeconds), stoppingToken); continue; }

                    var options = new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = _parallel, CancellationToken = stoppingToken };
                    var block = new ActionBlock<OutboxMessage>(async msg =>
                    {
                        try
                        {
                            var t = Type.GetType(msg.Type) ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(msg.Type)).FirstOrDefault(t2 => t2 != null);
                            if (t == null)
                            {
                                _logger.LogWarning("Outbox type not found: {Type}", msg.Type);
                                return;
                            }

                            var integrationEvent = JsonSerializer.Deserialize(msg.Content, t, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (integrationEvent == null)
                            {
                                _logger.LogWarning("Failed to deserialize outbox message {Id}", msg.Id);
                                return;
                            }

                            await publisher.Publish(integrationEvent, stoppingToken);

                            var ent = await db.OutboxMessages.FindAsync(new object[] { msg.Id }, cancellationToken: stoppingToken);
                            if (ent != null)
                            {
                                ent.Processed = true;
                                ent.ProcessedOn = DateTimeOffset.UtcNow;
                                await db.SaveChangesAsync(stoppingToken);
                                _logger.LogInformation("Published outbox {Id}", msg.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed publish outbox {Id}", msg.Id);
                            msg.RetryCount++;
                            db.OutboxMessages.Update(msg);
                            await db.SaveChangesAsync(stoppingToken);
                        }
                    }, options);

                    foreach (var m in msgs) block.Post(m);
                    block.Complete();
                    await block.Completion;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox loop exception");
                }

                await Task.Delay(TimeSpan.FromSeconds(_pollSeconds), stoppingToken);
            }
        }
    }
}
