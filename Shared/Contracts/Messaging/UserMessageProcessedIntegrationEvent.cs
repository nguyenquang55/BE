using System;

namespace Shared.Contracts.Messaging
{
    /// <summary>
    /// Event published by Worker after processing the user's message.
    /// </summary>
    public record UserMessageProcessedIntegrationEvent(
        string MessageId,
        string? UserId,
        string ResultType,
        string ConnectionId,
        object ProcessingResult,
        string TraceId,
        DateTimeOffset ProcessedAt
    );
}
