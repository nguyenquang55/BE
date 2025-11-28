namespace Shared.Common
{
    public static class RealtimeMessageTypes
    {
        public const string Ack = "ack";                 // server -> client: message received, enqueued
        public const string Preview = "preview";         // server -> client: preview payload for user confirmation
        public const string Processed = "processed";     // server -> client: final result payload
        public const string DecisionAccepted = "decision-ack"; // server -> client: confirm/cancel received and enqueued
        public const string Cancelled = "cancelled";     // server -> client: operation cancelled by user
        public const string Expired = "expired";         // server -> client: preview expired
        public const string Error = "error";             // server -> client: error payload
    }
}
