using System;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Abstractions.Services;
using Application.Contracts.ThirdParty.Calendar.Request;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Messaging;
using Shared.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Worker.Consumers
{
    public class UserPreviewDecisionConsumer : IConsumer<UserPreviewDecisionIntegrationEvent>
    {
        private readonly ICalendarService _calendarService;
        private readonly IPublishEndpoint _publisher;
        private readonly ILogger<UserPreviewDecisionConsumer> _logger;

        public UserPreviewDecisionConsumer(ICalendarService calendarService, IPublishEndpoint publisher, ILogger<UserPreviewDecisionConsumer> logger)
        {
            _calendarService = calendarService;
            _publisher = publisher;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<UserPreviewDecisionIntegrationEvent> context)
        {
            var evt = context.Message;

            object processingResult;

            try
            {
                if (!evt.Confirmed)
                {
                    processingResult = new { status = RealtimeMessageTypes.Cancelled, message = "User cancelled operation" };
                }
                else
                {
                    switch ((evt.ResultType ?? string.Empty).ToLowerInvariant())
                    {
                        case "create_event":
                            {
                                CreateEventExecutionPayload? payload = null;
                                if (evt.ExecutionPayload != null)
                                {
                                    var payloadJson = JsonSerializer.Serialize(evt.ExecutionPayload);

                                    var options = new JsonSerializerOptions
                                    {
                                        PropertyNameCaseInsensitive = true
                                    };

                                    payload = JsonSerializer.Deserialize<CreateEventExecutionPayload>(payloadJson, options);
                                }
                                if (payload == null)
                                    throw new InvalidOperationException("Invalid execution payload for create_event");

                                if (!Guid.TryParse(evt.UserId, out var userId))
                                    throw new InvalidOperationException("Invalid userId in decision event");

                                var res = await _calendarService.ExecuteCreateAsync(payload, userId);
                                processingResult = res;
                                break;
                            }
                        case "update_event":
                            {
                                UpdateEventExecutionPayload? payload = null;
                                if (evt.ExecutionPayload != null)
                                {
                                    var payloadJson = JsonSerializer.Serialize(evt.ExecutionPayload);
                                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                                    payload = JsonSerializer.Deserialize<UpdateEventExecutionPayload>(payloadJson, options);
                                }
                                if (payload == null || string.IsNullOrWhiteSpace(payload.EventId))
                                    throw new InvalidOperationException("Invalid execution payload for update_event");

                                if (!Guid.TryParse(evt.UserId, out var userId))
                                    throw new InvalidOperationException("Invalid userId in decision event");

                                var res = await _calendarService.ExecuteUpdateAsync(payload, userId);
                                processingResult = res;
                                break;
                            }
                        case "delete_event":
                            {
                                DeleteEventExecutionPayload? payload = null;
                                if (evt.ExecutionPayload != null)
                                {
                                    var payloadJson = JsonSerializer.Serialize(evt.ExecutionPayload);
                                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                                    payload = JsonSerializer.Deserialize<DeleteEventExecutionPayload>(payloadJson, options);
                                }
                                if (payload == null || string.IsNullOrWhiteSpace(payload.EventId))
                                    throw new InvalidOperationException("Invalid execution payload for delete_event");

                                if (!Guid.TryParse(evt.UserId, out var userId))
                                    throw new InvalidOperationException("Invalid userId in decision event");

                                var res = await _calendarService.ExecuteDeleteAsync(payload, userId);
                                processingResult = res;
                                break;
                            }
                        default:
                            throw new NotSupportedException($"Unsupported ResultType '{evt.ResultType}' for decision execution");
                    }
      
                }
            }
            catch (Exception ex)
            {
                processingResult = new { type = RealtimeMessageTypes.Error, error = ex.Message };
            }

            var processedEvt = new UserMessageProcessedIntegrationEvent(
                MessageId: evt.MessageId,
                UserId: evt.UserId,
                ResultType: evt.ResultType,
                ConnectionId: evt.ConnectionId,
                ProcessingResult: processingResult,
                TraceId: evt.TraceId,
                ProcessedAt: DateTimeOffset.UtcNow
            );
            await _publisher.Publish(processedEvt);
            _logger.LogInformation("Decision processed for message {MessageId}", evt.MessageId);
        }
    }
}
