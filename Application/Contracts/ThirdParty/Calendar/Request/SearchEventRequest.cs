using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Contracts.ThirdParty.Calendar.Request
{
    public class SearchEventRequest
    {
        public string Title { get; set; }
        public string StartDateTime { get; set; }
        public string EndDateTime { get; set; }
    }
}
