using System;
using System.Threading.Tasks;
using Application.Abstractions.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Messaging;
using Infrastructure.Consumers.Common;

namespace Worker.Consumers
{
    public class UserMessageSubmittedConsumer : TConsumer<UserMessageSubmittedIntegrationEvent>
    {
        private readonly IModelInferenceService _inference;
        private readonly IPublishEndpoint _publisher;
        private readonly ILogger<UserMessageSubmittedConsumer> _logger;

        public UserMessageSubmittedConsumer(IModelInferenceService inference, IPublishEndpoint publisher, ILogger<UserMessageSubmittedConsumer> logger)
        {
            _inference = inference;
            _publisher = publisher;
            _logger = logger;
        }

        public override async Task Consume(ConsumeContext<UserMessageSubmittedIntegrationEvent> context)
        {
            var evt = context.Message;
            var result = await _inference.InferAsync(evt.Payload);
            var processedEvt = new UserMessageProcessedIntegrationEvent(
                MessageId: evt.MessageId,
                UserId: evt.UserId,
                ConnectionId: evt.ConnectionId,
                ProcessingResult: result,
                TraceId: evt.TraceId,
                ProcessedAt: DateTimeOffset.UtcNow
            );
            await _publisher.Publish(processedEvt);
            _logger.LogInformation("Processed message {MessageId} trace {TraceId}", evt.MessageId, evt.TraceId);
        }
    }
}
