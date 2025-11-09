using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class RequestSlot : BaseEntity
    {
        public Guid RequestId { get; set; }
        public Request Request { get; set; } = default!;

        public string SlotName { get; set; } = default!;
        public string? Value { get; set; }
        public double? Confidence { get; set; }
    }
}
