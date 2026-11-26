namespace TutorFlow.Application.Ports.Clock;

/// <summary>Port to the system clock, so timestamps and durations are testable (R-LOG-01, R-LOG-08).</summary>
/// <remarks><b>Implemented by:</b> <c>plataforma</c>, in <c>TutorFlow.Infrastructure/Clock/</c> (DUDA-02).</remarks>
public interface IClock
{
    /// <summary>Current instant in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
