using Application.Abstractions.Services;
using Application.Service;
using Infrastructure.Model;
using Worker.Model;
using MassTransit;
using Shared.Contracts.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Tokenizer & Model inference singletons (init once)
builder.Services.AddSingleton<ITokenizerService, BertTokenizerService>();
builder.Services.AddSingleton<IModelInferenceService, OnnxModelInferenceService>();
// Backward-compatible processing service could wrap inference (or keep if used elsewhere)
builder.Services.AddSingleton<IMessageProcessingService, MessageProcessingService>();

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
