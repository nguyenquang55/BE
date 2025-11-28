using System;
using System.Collections.Generic;

namespace Application.Contracts.ThirdParty.Calendar.Respone
{
    public class CalendarOperationPreview
    {
        public string Action { get; set; } = "create"; // create|update|delete
        public string? Title { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public string? TargetEventId { get; set; } // for update/delete (not used in create)
        public List<string>? Warnings { get; set; }
        public object? ExecutionPayload { get; set; } // strongly-typed payload for execution phase
        public double? ConfidenceScore { get; set; } // from intent classification if needed
    }
}
