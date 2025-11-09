using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class Calendar : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = default!;

        public string? ProviderCalendarId { get; set; } // Google Calendar ID
        public string? Summary { get; set; }
        public string? Metadata { get; set; } // JSON

        public ICollection<UserSchedule> Events { get; set; } = new List<UserSchedule>();
    }
}
