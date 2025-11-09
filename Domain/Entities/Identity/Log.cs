using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class Log:BaseEntity
    {
        public DateTime Time { get; set; }    
        public string Message { get; set; } = default!;
    }
}
