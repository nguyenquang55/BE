using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Common
{
    // PSEUDOCODE:
    // - Define a reusable Vietnam TimeZoneInfo:
    //   - Try IANA id "Asia/Ho_Chi_Minh" (Linux/macOS).
    //   - Fallback to Windows id "SE Asia Standard Time" (Windows).
    //   - If both fail, fallback to a custom fixed UTC+07 zone.
    // - Provide a helper to get "now" in Vietnam time by converting from UtcNow.
    // - Initialize CreatedAt using the Vietnam "now" helper.
    public class BaseEntity
    {
        private static readonly TimeZoneInfo VietnamTimeZone = InitializeVietnamTimeZone();

        private static TimeZoneInfo InitializeVietnamTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); } catch { /* ignore */ }
            try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); } catch { /* ignore */ }
            // Fallback: fixed offset UTC+07 if specific time zone IDs are unavailable.
            return TimeZoneInfo.CreateCustomTimeZone(
                id: "UTC+07",
                baseUtcOffset: TimeSpan.FromHours(7),
                displayName: "UTC+07 Vietnam",
                standardDisplayName: "UTC+07"
            );
        }

        private static DateTimeOffset NowInVietnam() =>
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, VietnamTimeZone);

        public Guid Id { get; set; } = Guid.NewGuid();

        public DateTimeOffset CreatedAt { get; set; } = NowInVietnam();
        public string? CreatedBy { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        public DateTimeOffset? ExpiresAt { get; set; }

        private readonly List<BaseEvent> _domainEvents = new();
        [NotMapped]
        public IReadOnlyCollection<BaseEvent> DomainEvents => _domainEvents.AsReadOnly();

        public void AddDomainEvent(BaseEvent domainEvent) => _domainEvents.Add(domainEvent);
        public void RemoveDomainEvent(BaseEvent domainEvent) => _domainEvents.Remove(domainEvent);
        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
