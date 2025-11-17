using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Contracts.ThirdParty.Calendar.Request
{
    public class DeleteEventRequest
    {
        string? Title { get; set; }
        DateTime StartTime { get; set; }
        DateTime EndTime { get; set; }
        bool IsAllDay { get; set; }
    }
}
