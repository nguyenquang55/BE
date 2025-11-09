using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth.Request
{
    public class RefreshAuthTokenRequest
    {
        public string? SessionToken { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
    }
}
