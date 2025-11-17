using System;
using System.Threading.Tasks;
using Application.Abstractions.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Messaging;
using Infrastructure.Consumers.Common;
using Application.Abstractions.Infrastructure;

namespace Worker.Consumers
{
    public class UserMessageSubmittedConsumer : TConsumer<UserMessageSubmittedIntegrationEvent>
    {
        private readonly IModelInferenceService _inference;
        private readonly IPublishEndpoint _publisher;
        private readonly IRedisCacheService _redisCacheService;
        public readonly ILLMService _llmService;
        private readonly ILogger<UserMessageSubmittedConsumer> _logger;

        public UserMessageSubmittedConsumer(IModelInferenceService inference,IPublishEndpoint publisher, ILogger<UserMessageSubmittedConsumer> logger,IRedisCacheService redisCacheService,ILLMService lLMService)
        {
            _llmService = lLMService;
            _redisCacheService = redisCacheService;
            _inference = inference;
            _publisher = publisher;
            _logger = logger;
        }

        public override async Task Consume(ConsumeContext<UserMessageSubmittedIntegrationEvent> context)
        {
            var evt = context.Message;

            //var userId = await _redisCacheService.GetAsync<Guid>($"UserID:{evt.sessionToken}");



            var Mbertresult = await _inference.InferAsync(evt.Payload);
            if (!Guid.TryParse(evt.userId, out Guid userIdAsGuid))
            {
                _logger.LogWarning("Invalid UserId format: {UserId} for MessageId: {MessageId}",
                    evt.userId, evt.MessageId);
                throw new ArgumentException($"User ID không hợp lệ: {evt.userId}");
            }

            var Result = await _llmService.ChooseFuction(Mbertresult, userIdAsGuid);

            var processedEvt = new UserMessageProcessedIntegrationEvent(
                MessageId: evt.MessageId,
                UserId: evt.userId,
                ConnectionId: evt.ConnectionId,
                ProcessingResult: Mbertresult,
                TraceId: evt.TraceId,
                ProcessedAt: DateTimeOffset.UtcNow
            );
            await _publisher.Publish(processedEvt);
            _logger.LogInformation("Processed message {MessageId} trace {TraceId}", evt.MessageId, evt.TraceId);
        }
    }
}
