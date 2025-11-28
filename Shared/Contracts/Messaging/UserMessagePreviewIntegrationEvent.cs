using System;

namespace Shared.Contracts.Messaging
{
    public record UserMessagePreviewIntegrationEvent(
        string MessageId,
        string? UserId,
        string ConnectionId,
        string ResultType,
        object PreviewPayload,
        string TraceId,
        DateTimeOffset ExpiresAt
    );
}
