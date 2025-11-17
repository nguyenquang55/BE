using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Contracts.ThirdParty.Email.Request
{
    public class SearchEmailRequest
    {
        public string? SearchTitle { get; set; }
        public int MaxResults { get; set; }
    }
}
