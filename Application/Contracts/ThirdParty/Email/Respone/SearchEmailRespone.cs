using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Contracts.ThirdParty.Email.Respone
{
    public class SearchEmailRespone
    {
        public Guid Id { get; set; }
        public string? ThreadId { get; set; }
        public string? From { get; set; }
        public string? Subject { get; set; }
        public DateTime Date { get; set; }
        public string? Snippet { get; set; }
        public string? Body { get; set; }
    }
}