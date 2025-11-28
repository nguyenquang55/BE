using System;

namespace Application.Contracts.ThirdParty.Calendar.Request
{
    public class CreateEventExecutionPayload
    {
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}
