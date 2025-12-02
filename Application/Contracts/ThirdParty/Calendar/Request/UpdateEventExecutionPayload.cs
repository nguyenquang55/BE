using System;

namespace Application.Contracts.ThirdParty.Calendar.Request
{
    public class UpdateEventExecutionPayload
    {
        public string EventId { get; set; } = string.Empty;
        public string? NewTitle { get; set; }
        public DateTime? NewStart { get; set; }
        public DateTime? NewEnd { get; set; }
        public string? ETag { get; set; }
    }
}
