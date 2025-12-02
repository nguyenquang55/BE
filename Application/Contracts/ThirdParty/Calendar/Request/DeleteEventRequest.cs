using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Contracts.ThirdParty.Calendar.Request
{
    public class DeleteEventRequest
    {
        string Title { get; set; }
        string StartDateTime { get; set; }
        string EndDateTime { get; set; }
    }
}
