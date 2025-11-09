using System;

namespace Shared.Common
{
    /// <summary>
    /// Base class cho các integration events.
    /// </summary>
    public abstract class IntegrationEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime OccurredOn { get; set; } = DateTime.UtcNow;
    }
}
