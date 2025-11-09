using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Contracts.Auth.Request
{
    public class SendOtpRequest
    {
        public string Email { get; set; } = null!;
    }
}
