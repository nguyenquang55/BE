using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Contracts.ThirdParty.Email.Request
{
    public class ForwardEmailRequest
    {
        public string? EmailId { get; set; }
        public string? Title { get; set; }
        public string? To { get; set; }
        public string? Subject { get; set; }
        public string? Body { get; set; }
    }
}
