using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class Request : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = default!;
        public string RequestType { get; set; }
        public string? Transcript { get; set; }
        public string? Intent { get; set; }
        public double? IntentConfidence { get; set; }
        public string? RawPayload { get; set; }
        public string Status { get; set; }
        public ICollection<RequestSlot> Slots { get; set; } = new List<RequestSlot>();
    }
}
