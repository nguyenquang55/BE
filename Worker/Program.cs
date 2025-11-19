using Application.Abstractions.Common;
using Application.Abstractions.Infrastructure;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Service;
using Ecom.Infrastructure.Persistence;
using Infrastructure.Cache;
using Infrastructure.DI;
using Infrastructure.Model;
using Infrastructure.Persistence.DatabaseContext;
using Infrastructure.Persistence.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Messaging;
using StackExchange.Redis;
using Worker.Model;
using static Org.BouncyCastle.Math.EC.ECCurve;

var builder = WebApplication.CreateBuilder(args);



// Tokenizer & Model inference singletons (init once)
builder.Services.AddSingleton<ITokenizerService, BertTokenizerService>();
builder.Services.AddSingleton<IModelInferenceService, OnnxModelInferenceService>();
// Backward-compatible processing service could wrap inference (or keep if used elsewhere)
builder.Services.AddSingleton<IMessageProcessingService, MessageProcessingService>();
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect("localhost:6379"));
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();
builder.Services.AddTransient<IOAuthTokenService,OAuthTokenService>();
builder.Services.AddTransient<ILLMService, LLMService>();
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddHttpClient<IGeminiClient, GeminiClient>();
builder.Services.AddHttpClient<ICalendarService, CalendarService>();
builder.Services.AddTransient<ICalendarService, CalendarService>();
builder.Services.AddTransient<IOAuthRepository, OAuthRepository>();
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddDbContext<ApplicationDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMassTransit(x =>
{
	x.AddConsumer<Worker.Consumers.UserMessageSubmittedConsumer>();
	x.UsingRabbitMq((context, cfgMq) =>
	{
		var mqHost = builder.Configuration.GetValue<string>("RabbitMq:Host", "localhost");
		var mqUser = builder.Configuration.GetValue<string>("RabbitMq:Username", "guest");
		var mqPass = builder.Configuration.GetValue<string>("RabbitMq:Password", "guest");
		var mqVHost = builder.Configuration.GetValue<string>("RabbitMq:VirtualHost", "/");

		if (mqHost.Contains("://"))
			cfgMq.Host(new Uri(mqHost), h => { h.Username(mqUser); h.Password(mqPass); });
		else
			cfgMq.Host(mqHost, mqVHost, h => { h.Username(mqUser); h.Password(mqPass); });

		cfgMq.ReceiveEndpoint("user.msg.submitted.queue", e =>
		{
			e.PrefetchCount = 1; // sequential processing
			e.ConfigureConsumer<Worker.Consumers.UserMessageSubmittedConsumer>(context);
		});
	});
});

var app = builder.Build();

app.MapGet("/health", () => "ok");

// Warm-up heavy singletons
_ = app.Services.GetService<ITokenizerService>();
_ = app.Services.GetService<IModelInferenceService>();

app.Run();
