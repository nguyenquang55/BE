using System;

namespace Application.Contracts.Contact
{
    public class ContactDTO
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Source { get; set; }
    }
}
