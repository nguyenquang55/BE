using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class Session:BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = default!;
        public string? SessionToken { get; set; }
        public bool IsRevoked { get; set; } = false;
    }
}
