using System;

namespace Shared.Contracts.Messaging
{
    /// <summary>
    /// Event published by Worker after processing the user's message.
    /// </summary>
    public record UserMessageProcessedIntegrationEvent(
        string MessageId,
        string? UserId,
        string ConnectionId,
        string ProcessingResult,
        string TraceId,
        DateTimeOffset ProcessedAt
    );
}
