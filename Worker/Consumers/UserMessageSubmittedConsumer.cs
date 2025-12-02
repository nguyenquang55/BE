using Application.Abstractions.Infrastructure;
using Application.Abstractions.Services;
using Application.Contracts.Contact;
using Infrastructure.Consumers.Common;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Messaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Worker.Consumers
{
    public class UserMessageSubmittedConsumer : TConsumer<UserMessageSubmittedIntegrationEvent>
    {
        private readonly IModelInferenceService _inference;
        private readonly IPublishEndpoint _publisher;
        private readonly IRedisCacheService _redisCacheService;
        public readonly ILLMService _llmService;
        private readonly IGeminiClient _geminiClient;
        private readonly ILogger<UserMessageSubmittedConsumer> _logger;

        public UserMessageSubmittedConsumer(
            IModelInferenceService inference,
            IPublishEndpoint publisher,
            ILogger<UserMessageSubmittedConsumer> logger,
            IRedisCacheService redisCacheService,
            ILLMService lLMService,
            IGeminiClient geminiClient)
        {
            _geminiClient = geminiClient;
            _llmService = lLMService;
            _redisCacheService = redisCacheService;
            _inference = inference;
            _publisher = publisher;
            _logger = logger;
        }

        public override async Task Consume(ConsumeContext<UserMessageSubmittedIntegrationEvent> context)
        {
            var evt = context.Message;

            var Mbertresult = await _inference.InferAsync(evt.Payload);
            if (!Guid.TryParse(evt.userId, out Guid userIdAsGuid))
            {
                _logger.LogWarning("Invalid UserId format: {UserId} for MessageId: {MessageId}",
                    evt.userId, evt.MessageId);
                throw new ArgumentException($"Invalid User ID: {evt.userId}");
            }

            await Task.Delay(500);

            if (string.Equals(Mbertresult.Intent, "create_event", StringComparison.OrdinalIgnoreCase))
            {
                var result = await _llmService.ChooseFuction(Mbertresult, userIdAsGuid, true);

                var previewEvt = new UserMessagePreviewIntegrationEvent(
                    MessageId: evt.MessageId,
                    UserId: evt.userId,
                    ConnectionId: evt.ConnectionId,
                    ResultType: Mbertresult.Intent,
                    PreviewPayload: result,
                    TraceId: evt.TraceId,
                    ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5)
                );

                await _publisher.Publish(previewEvt);
                _logger.LogInformation("Published preview for message {MessageId} trace {TraceId}", evt.MessageId, evt.TraceId);
                return;
            }
            else if (
                string.Equals(Mbertresult.Intent, "update_event", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Mbertresult.Intent, "delete_event", StringComparison.OrdinalIgnoreCase))
            {
                // Ask for a preview; if service reports not found, send processed instead
                var previewOrFailure = await _llmService.ChooseFuction(Mbertresult, userIdAsGuid, true);

                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(previewOrFailure);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Success", out var successEl) && successEl.ValueKind == System.Text.Json.JsonValueKind.False)
                    {
                        string message = doc.RootElement.TryGetProperty("Message", out var msgEl) && msgEl.ValueKind == System.Text.Json.JsonValueKind.String
                            ? msgEl.GetString()!
                            : "Không tìm thấy sự kiện phù hợp";

                        var processedNotFound = new UserMessageProcessedIntegrationEvent(
                            MessageId: evt.MessageId,
                            UserId: evt.userId,
                            ResultType: Mbertresult.Intent,
                            ConnectionId: evt.ConnectionId,
                            ProcessingResult: new { status = "not_found", message },
                            TraceId: evt.TraceId,
                            ProcessedAt: DateTimeOffset.UtcNow
                        );
                        await _publisher.Publish(processedNotFound);
                        _logger.LogInformation("No target events found for {Intent} - message {MessageId}", Mbertresult.Intent, evt.MessageId);
                        return;
                    }
                }
                catch
                {
                    // Ignore JSON parse errors, continue to publish preview event
                }

                var previewEvt = new UserMessagePreviewIntegrationEvent(
                    MessageId: evt.MessageId,
                    UserId: evt.userId,
                    ConnectionId: evt.ConnectionId,
                    ResultType: Mbertresult.Intent,
                    PreviewPayload: previewOrFailure,
                    TraceId: evt.TraceId,
                    ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5)
                );

                await _publisher.Publish(previewEvt);
                _logger.LogInformation("Published preview for {Intent} message {MessageId} trace {TraceId}", Mbertresult.Intent, evt.MessageId, evt.TraceId);
                return;
            }
            else
            {
                var result = await _llmService.ChooseFuction(Mbertresult, userIdAsGuid);
                var processedEvt = new UserMessageProcessedIntegrationEvent(
                    MessageId: evt.MessageId,
                    UserId: evt.userId,
                    ResultType: Mbertresult.Intent,
                    ConnectionId: evt.ConnectionId,
                    ProcessingResult: result,
                    TraceId: evt.TraceId,
                    ProcessedAt: DateTimeOffset.UtcNow
                );
                await _publisher.Publish(processedEvt);
                _logger.LogInformation("Published processed for message {MessageId} trace {TraceId}", evt.MessageId, evt.TraceId);
            }
        }
    } 
}
