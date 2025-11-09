using System;
using System.ComponentModel.DataAnnotations;

namespace Application.Contracts.Contact
{
    public class UpdateContactRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public string? Source { get; set; }
    }
}
