using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class OAuthToken:BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid AuthProviderId { get; set; }
        public OAuthProvider AuthProvider { get; set; } = default!;
        public string Provider { get; set; } = "google";
        public string? Scopes { get; set; }
        public string RefreshToken { get; set; }
    }
}
