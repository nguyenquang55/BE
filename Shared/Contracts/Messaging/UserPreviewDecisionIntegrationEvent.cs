using System;

namespace Shared.Contracts.Messaging
{
    /// <summary>
    /// Event published when the user confirms or cancels a previewed operation.
    /// </summary>
    public record UserPreviewDecisionIntegrationEvent(
        string MessageId,
        string? UserId,
        string ConnectionId,
        string ResultType,
        bool Confirmed,
        object? ExecutionPayload,
        string TraceId,
        DateTimeOffset DecidedAt
    );
}
