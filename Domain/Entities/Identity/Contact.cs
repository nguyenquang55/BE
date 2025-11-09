using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class Contact : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = default!;
        public string Name { get; set; } = default!; 
        public string Email { get; set; } = default!;
        public string? Source { get; set; } = "manual";         
    }
}
