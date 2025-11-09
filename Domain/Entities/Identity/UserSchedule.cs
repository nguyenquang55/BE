using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class UserSchedule : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = default!;

        public Guid CalendarId { get; set; }
        public Calendar Calendar { get; set; } = default!;

        public string? ProviderEventId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }

        public string? Attendees { get; set; } 
        public string? Location { get; set; }
        public string? Reminders { get; set; } 
        public string? Metadata { get; set; } 
    }
}
