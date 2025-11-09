using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth.Response
{
    public class LoginResponse
    {
        //public string AccessToken { get; set; } = default!;
        //public DateTime TokenExpiresAt { get; set; }
        //public string RefreshToken { get; set; } = default!; // return plaintext only once
        //public DateTime RefreshTokenExpiresAt { get; set; }
        public string sessionToken { get; set; } = default!;
        public UserDetailDTO? User { get; set; }
    }
}
