using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class OAuthProvider:BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = default!;

        public string Provider { get; set; } = default!; // ví dụ: 'google'
        public string? ProviderUserId { get; set; } // 'sub' trong JWT của Google
        public string? ProviderEmail { get; set; } // email của tài khoản Google đó
        public string? DisplayName { get; set; } // tên tài khoản Google
        public bool IsPrimary { get; set; } = false; // có thể đánh dấu email chính

        public DateTimeOffset LinkedAt { get; set; } = DateTimeOffset.UtcNow;

        public ICollection<OAuthToken> Tokens { get; set; } = new List<OAuthToken>();
    }
}