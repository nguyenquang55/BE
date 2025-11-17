using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Contracts.ThirdParty.Calendar.Request
{
    public class UpdateEventRequest
    {
        string? Title { get; set; }
        string? Description { get; set; }
        string? Location { get; set; }
        TimeOnly StartTime { get; set; }
        TimeOnly EndTime { get; set; }
        DateTime Date { get; set; }
        bool IsAllDay { get; set; }
        string? TimeZone { get; set; }
        string[]? Attendees { get; set; }
    }
}
