using TutorFlow.Application.Ports.Clock;

namespace TutorFlow.Infrastructure.Clock;

/// <summary>System clock adapter for <see cref="IClock"/>; always reports UTC (DUDA-02).</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
