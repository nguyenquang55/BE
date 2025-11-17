using System;

namespace Shared.Contracts.Messaging
{
    /// <summary>
    /// Event published when a user submits a message via SignalR.
    /// </summary>
    public record UserMessageSubmittedIntegrationEvent(
        string MessageId,
        string? userId,
        string ConnectionId,
        string Payload,
        string TraceId,
        DateTimeOffset CreatedAt
    );
}
