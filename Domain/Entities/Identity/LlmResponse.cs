using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class ResponseFromLmmModel:BaseEntity
    {
        public Guid? UserId { get; set; }
        public User? User { get; set; }

        public string SourceType { get; set; } = default!; 
        public string SourceRef { get; set; } = default!;
        public string? Model { get; set; } 
        public string? Content { get; set; }
        public int? TokensUsed { get; set; }
        public DateTimeOffset? CachedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? TtlUntil { get; set; }
    }
}
