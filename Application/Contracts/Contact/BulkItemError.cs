using System;

namespace Application.Contracts.Contact
{
    public class BulkItemError
    {
        public int Index { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
