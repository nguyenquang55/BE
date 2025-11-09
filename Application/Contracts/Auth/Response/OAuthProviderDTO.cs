using System;

namespace Application.DTOs.Auth.Response
{
    public class OAuthProviderDTO
    {
        public Guid Id { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string? ProviderUserId { get; set; }
        public string? ProviderEmail { get; set; }
        public string? DisplayName { get; set; }
        public bool IsPrimary { get; set; }
        public DateTimeOffset LinkedAt { get; set; }
        // Note: Tokens are intentionally omitted for security
    }
}
