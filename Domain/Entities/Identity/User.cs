using Domain.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class User:BaseEntity
    {
        public string Email { get; set; } = default!;
        public string? DisplayName { get; set; }
        public string? PasswordHash { get; set; }
        public string? Timezone { get; set; } 
        public bool IsActive { get; set; } = true;

        public ICollection<OAuthProvider> AuthProviders { get; set; } = new List<OAuthProvider>();
        public ICollection<OAuthToken> OAuthTokens { get; set; } = new List<OAuthToken>();
        public ICollection<Calendar> Calendars { get; set; } = new List<Calendar>();
        public ICollection<EmailThread> EmailThreads { get; set; } = new List<EmailThread>();
        public ICollection<Request> Requests { get; set; } = new List<Request>();
    }
}
