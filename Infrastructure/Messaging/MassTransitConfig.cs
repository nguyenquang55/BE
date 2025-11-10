using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System;

namespace Infrastructure.Messaging
{
    /// <summary>
    /// Lớp cấu hình tĩnh cho MassTransit, tích hợp với RabbitMQ để quản lý messaging trong hệ thống.
    /// Cung cấp các phương thức để đăng ký publisher và consumer cho RabbitMQ.
    /// </summary>
    public static class MassTransitConfig
    {
    /// <summary>
    /// Đăng ký MassTransit cho vai trò Publisher (WebApi).
    /// Chỉ tạo bus để publish message (IPublishEndpoint), không định nghĩa queue vì Publisher chỉ gửi message.
    /// Sử dụng cấu hình RabbitMQ từ appsettings (Host, Username, Password).
    /// </summary>
    public static void AddMassTransitPublisher(IServiceCollection services, IConfiguration cfg)
        {
            var mqHost = cfg.GetValue<string>("RabbitMq:Host", "rabbitmq");
            var mqUser = cfg.GetValue<string>("RabbitMq:Username", "guest");
            var mqPass = cfg.GetValue<string>("RabbitMq:Password", "guest");

            services.AddMassTransit(x =>
            {
                x.UsingRabbitMq((context, rcfg) =>
                {
                    rcfg.Host(mqHost, h =>
                    {
                        h.Username(mqUser);
                        h.Password(mqPass);
                    });

                    // Do not define queues here for publisher
                });
            });
        }

        /// <summary>
        /// Đăng ký MassTransit cho vai trò Consumer (Worker).
        /// Tự động tạo endpoint theo cấu hình RabbitMq:Exchanges trong appsettings.
        /// Cho phép custom từng endpoint qua delegate configurePerEndpoint.
        /// Cấu hình prefetch, retry (số lần và khoảng thời gian).
        /// Bind queue vào exchange theo type (fanout, direct, topic...) và routing key.
        /// </summary>
        public static void AddMassTransitConsumers(IServiceCollection services, IConfiguration cfg, Action<IBusRegistrationContext, IRabbitMqReceiveEndpointConfigurator, string>? configurePerEndpoint = null)
        {
            var mqHost = cfg.GetValue<string>("RabbitMq:Host", "rabbitmq");
            var mqUser = cfg.GetValue<string>("RabbitMq:Username", "guest");
            var mqPass = cfg.GetValue<string>("RabbitMq:Password", "guest");
            var prefetch = cfg.GetValue<ushort>("RabbitMq:PrefetchCount", 16);
            var retryCount = cfg.GetValue<int>("RabbitMq:Retry:RetryCount", 5);
            var retryInterval = cfg.GetValue<int>("RabbitMq:Retry:IntervalSeconds", 5);

            services.AddMassTransit(x =>
            {
                // Consumers need to be added by caller: x.AddConsumers(typeof(YourConsumerAssembly).Assembly)
                x.UsingRabbitMq((context, rcfg) =>
                {
                    rcfg.Host(mqHost, h =>
                    {
                        h.Username(mqUser);
                        h.Password(mqPass);
                    });

                    var exchanges = cfg.GetSection("RabbitMq:Exchanges").GetChildren();
                    foreach (var ex in exchanges)
                    {
                        var exchangeName = ex.GetValue<string>("Name", ex.Key.ToLowerInvariant());
                        var exchangeType = ex.GetValue<string>("Type", ExchangeType.Topic);
                        var queueName = ex.GetValue<string>("Queue", $"{exchangeName}.queue");
                        var routingKey = ex.GetValue<string>("RoutingKey", "");

                        rcfg.ReceiveEndpoint(queueName, e =>
                        {
                            e.ConfigureConsumeTopology = false;
                            e.PrefetchCount = prefetch;
                            e.UseMessageRetry(r => r.Interval(retryCount, TimeSpan.FromSeconds(retryInterval)));

                            e.Bind(exchangeName, b =>
                            {
                                b.ExchangeType = exchangeType;
                                if (!string.IsNullOrWhiteSpace(routingKey))
                                    b.RoutingKey = routingKey;
                            });

                            configurePerEndpoint?.Invoke(context, e, ex.Key);
                        });
                    }
                });
            });
        }
    }
}
