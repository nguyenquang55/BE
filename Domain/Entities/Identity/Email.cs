using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Identity
{
    public class Email:BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = default!;

        public Guid? ThreadId { get; set; }
        public EmailThread? Thread { get; set; }

        public string? ProviderMessageId { get; set; }
        public string? FromAddress { get; set; }
        public string? ToAddresses { get; set; } // JSON array
        public string? CcAddresses { get; set; } // JSON array
        public string? Subject { get; set; }
        public string? Snippet { get; set; }
        public string? Headers { get; set; } // JSON
        public DateTimeOffset? ReceivedAt { get; set; }
        public string? Metadata { get; set; } // JSON
        public bool BodyStored { get; set; } = false;
    }
}
