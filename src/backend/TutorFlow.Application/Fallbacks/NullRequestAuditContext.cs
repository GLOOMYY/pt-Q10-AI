using TutorFlow.Application.Assistant;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Fallbacks;

/// <summary>
/// Default <see cref="IRequestAuditContext"/> while the real one (seguridad, <c>Application/Audit/</c>) is not
/// registered: it keeps nothing. Registered with <c>TryAdd</c> by <see cref="DependencyInjection.AddApplication"/> only
/// so the use case can be composed; the real context replaces it.
/// </summary>
public sealed class NullRequestAuditContext : IRequestAuditContext
{
    private string _correlationId = string.Empty;
    private DateTimeOffset _startedAt;

    /// <inheritdoc />
    public void Start(string correlationId, DateTimeOffset startedAt)
    {
        _correlationId = correlationId;
        _startedAt = startedAt;
    }

    /// <inheritdoc />
    public void SetRequest(string? question, AuditUser user)
    {
    }

    /// <inheritdoc />
    public void RecordResource(string route)
    {
    }

    /// <inheritdoc />
    public void RecordStudentConsulted(StudentId studentId)
    {
    }

    /// <inheritdoc />
    public void RecordTool(string toolName)
    {
    }

    /// <inheritdoc />
    public void Complete(AssistantAnswer answer)
    {
    }

    /// <inheritdoc />
    public AuditRecord BuildRecord(int status, DateTimeOffset finishedAt) => new()
    {
        Timestamp = _startedAt,
        CorrelationId = _correlationId,
        User = new AuditUser(null, null),
        Question = null,
        StudentsConsulted = [],
        ResourcesConsulted = [],
        Tools = [],
        Citations = [],
        Status = status,
        Reason = null,
        Tokens = 0,
        CostUsd = 0m,
        DurationMs = (long)Math.Max(0, (finishedAt - _startedAt).TotalMilliseconds),
    };
}
