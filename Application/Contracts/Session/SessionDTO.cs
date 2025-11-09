using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Contracts.Session
{
    public class SessionDTO
    {
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public DateTimeOffset ExpireAt{ get; set; }
        public bool IsRevoked { get; set; }
        public string? DeviceId { get; set; }
        public string ipAddress { get; set; } = string.Empty;
        public string userAgent { get; set; } = string.Empty;
    }
}
