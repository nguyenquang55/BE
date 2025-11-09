using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class EmailThread : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = default!;

        public string? ThreadProviderId { get; set; }
        public string? Snippet { get; set; }
        public string? Labels { get; set; } 
        public DateTimeOffset? LastMessageAt { get; set; }

        public ICollection<Email> Emails { get; set; } = new List<Email>();
    }
}
