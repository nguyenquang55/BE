using Application.Abstractions.Services;
using Application.Service;
using MassTransit;
using Shared.Contracts.Messaging;

var builder = WebApplication.CreateBuilder(args);

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

app.Run();
