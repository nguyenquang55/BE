using System;
using System.Collections.Generic;

namespace Application.DTOs.Auth.Response
{
    public class UserDetailDTO
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Timezone { get; set; }
        public bool IsActive { get; set; }
        public List<OAuthProviderDTO> AuthProviders { get; set; } = new();
    }
}
