using Application.Abstractions.Services;
using Application.Abstractions.SignalR;
using Application.Service;
using Infrastructure.BackgroundServices;
using Infrastructure.Cache;
using Infrastructure.Messaging;
using Infrastructure.Model;
using Infrastructure.Outbox;
using Infrastructure.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BE.DI
{
    public static class WebServiceRegistration
    {
        public static IServiceCollection AddWebServices(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Controllers & JSON settings
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                });

            // 2. Swagger Gen
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            // 3. SignalR with JSON and MessagePack protocols
            services.AddSignalR()
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                })
                .AddMessagePackProtocol();

            // 4. SignalR adapters and contexts
            services.AddSingleton<INotificationHubContext, BE.SignalR.NotificationHubContext>();
            services.AddSingleton<INotificationHub, NotificationHubAdapter>();

            // 5. HttpClients for Third Party API Services
            services.AddHttpClient<IGeminiClient, GeminiClient>();
            services.AddHttpClient<ICalendarService, CalendarService>();

            // 6. Messaging, Caching & Tokenizer Singletons
            services.AddSingleton<IMessageProcessingService, MessageProcessingService>();
            services.AddSingleton<IRoutingStore, RedisRoutingStore>();
            services.AddScoped<IMessageEnqueueService, MessageEnqueueService>();
            services.AddSingleton<ITokenizerService, BertTokenizerService>();
            services.AddSingleton<IRedisHealthCheckService, RedisHealthCheckService>();

            // 7. Hosted Background Services
            services.AddHostedService<RedisHealthCheckBgrService>();
            services.AddHostedService<CallendarEvntNotificationBgrService>();
            services.AddHostedService<CalendarCacheRefreshBgrService>();
            services.AddHostedService<OutboxPublisherService>();

            // 8. CORS Config
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            return services;
        }
    }
}
