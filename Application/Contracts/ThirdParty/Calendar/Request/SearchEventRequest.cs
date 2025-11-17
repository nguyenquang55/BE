using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Contracts.ThirdParty.Calendar.Request
{
    public class SearchEventRequest
    {
        string? Id { get; set; }
        string? Title { get; set; }
        DateTime StartDate { get; set; }
        DateTime EndDate { get; set; }
    }
}
