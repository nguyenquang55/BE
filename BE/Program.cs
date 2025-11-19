using Application.Abstractions.Services;
using Application.Service;
using Infrastructure.DI;
using Application.Abstractions.SignalR;
using Infrastructure.SignalR;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shared.Contracts.Messaging;
using MassTransit;
using BE.Hubs;
using System;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);



// sau khi đăng ký IRedisHealthCheckService
builder.Services.AddSingleton<IRedisHealthCheckService, RedisHealthCheckService>();

// đăng ký hosted service tường minh
builder.Services.AddHostedService<Infrastructure.BackgroundServices.RedisHealthCheckBgrService>();


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SignalR with JSON protocol (optimized) and MessagePack for bandwidth saving
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    })
    .AddMessagePackProtocol();
// Register SignalR adapter/context
builder.Services.AddSingleton<INotificationHubContext, BE.SignalR.NotificationHubContext>();
builder.Services.AddSingleton<INotificationHub, Infrastructure.SignalR.NotificationHubAdapter>();

// Message processing and enqueue services
builder.Services.AddSingleton<IMessageProcessingService, Application.Service.MessageProcessingService>();
builder.Services.AddSingleton<IRoutingStore, Infrastructure.Cache.RedisRoutingStore>();
builder.Services.AddScoped<IMessageEnqueueService, Infrastructure.Messaging.MessageEnqueueService>();
builder.Services.AddHttpClient<IGeminiClient, GeminiClient>(); 
builder.Services.AddHttpClient<ICalendarService, CalendarService>();
builder.Services.AddSingleton<ITokenizerService, Infrastructure.Model.BertTokenizerService>();



// MassTransit unified configuration: publish submitted, consume processed
builder.Services.AddMassTransit(x =>
{
    // Consumer for processed messages -> send via SignalR
    x.AddConsumer<UserMessageProcessedConsumer>();
    x.UsingRabbitMq((context, cfgMq) =>
    {
        var mqHost = builder.Configuration.GetValue<string>("RabbitMq:Host", "localhost");
        var mqUser = builder.Configuration.GetValue<string>("RabbitMq:Username", "guest");
        var mqPass = builder.Configuration.GetValue<string>("RabbitMq:Password", "guest");
        var mqVHost = builder.Configuration.GetValue<string>("RabbitMq:VirtualHost", "/");

        // Simple host (no port) or full URI
        if (mqHost.Contains("://"))
        {
            cfgMq.Host(new Uri(mqHost), h => { h.Username(mqUser); h.Password(mqPass); });
        }
        else
        {
            cfgMq.Host(mqHost, mqVHost, h => { h.Username(mqUser); h.Password(mqPass); });
        }

        // Receive processed event queue
        cfgMq.ReceiveEndpoint("user.msg.processed.queue", e =>
        {
            e.ConfigureConsumer<UserMessageProcessedConsumer>(context);
        });
    });
});

// Outbox hosted service (publishes submitted events)
builder.Services.AddHostedService<Infrastructure.Outbox.OutboxPublisherService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        // For token-based (no credentials from browser): allow any origin
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// IMPORTANT: call UseCors BEFORE authentication/authorization so preflight isn't blocked
// Choose policy: if your FE uses cookies, use "FrontendWithCredentials", otherwise "AllowAll"
app.UseCors("AllowAll");
// app.UseCors("FrontendWithCredentials");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map SignalR hubs
app.MapHub<BE.Hubs.NotificationHub>("/hubs/notifications");
// Warm-up: force creation of tokenizer at startup (optional)
_ = app.Services.GetService<ITokenizerService>();

app.Run();
