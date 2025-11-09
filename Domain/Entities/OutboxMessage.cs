using Domain.Common;
using System;

namespace Domain.Entities
{
    public class OutboxMessage : BaseEntity
    {
        public string Type { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTimeOffset OccurredOn { get; set; }
        public DateTimeOffset? ProcessedOn { get; set; }
        public string? Error { get; set; }
        public bool Processed { get; set; } = false;
        public int RetryCount { get; set; } = 0;
    }
}
 