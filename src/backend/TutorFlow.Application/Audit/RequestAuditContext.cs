using TutorFlow.Application.Assistant;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Audit;

/// <summary>
/// Scoped implementation of <see cref="IRequestAuditContext"/>: collects what really happened during one request so the
/// single audit record is truthful (R-LOG-01, R-LOG-04). It only stores what R-LOG-08 allows: ids, routes, tool names,
/// citations and counters. Never grades, absences, averages, names or the answer text (R-LOG-02, D-09).
/// </summary>
public sealed class RequestAuditContext : IRequestAuditContext
{
    private readonly object _gate = new();
    private readonly List<string> _resources = [];
    private readonly List<string> _students = [];
    private readonly List<string> _tools = [];
    private string? _correlationId;
    private DateTimeOffset _startedAt;
    private string? _question;
    private AuditUser _user = new(null, null);
    private AssistantAnswer? _answer;

    /// <inheritdoc />
    public void Start(string correlationId, DateTimeOffset startedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        lock (_gate)
        {
            _correlationId = correlationId;
            _startedAt = startedAt.ToUniversalTime();
        }
    }

    /// <inheritdoc />
    public void SetRequest(string? question, AuditUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        lock (_gate)
        {
            // D-27: the question is kept exactly as received, not trimmed nor masked.
            _question = question;
            _user = user;
        }
    }

    /// <inheritdoc />
    public void RecordResource(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        lock (_gate)
        {
            AddDistinct(_resources, route);
        }
    }

    /// <inheritdoc />
    public void RecordStudentConsulted(StudentId studentId)
    {
        ArgumentNullException.ThrowIfNull(studentId);
        lock (_gate)
        {
            AddDistinct(_students, studentId.Value);
        }
    }

    /// <inheritdoc />
    public void RecordTool(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        lock (_gate)
        {
            // Repetitions are kept: the record lists every invocation in order (R-ARQ-04).
            _tools.Add(toolName);
        }
    }

    /// <inheritdoc />
    public void Complete(AssistantAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        lock (_gate)
        {
            _answer = answer;
        }
    }

    /// <inheritdoc />
    public AuditRecord BuildRecord(int status, DateTimeOffset finishedAt)
    {
        lock (_gate)
        {
            if (_correlationId is null)
            {
                throw new InvalidOperationException("The audit context was not started: call Start first.");
            }

            var elapsed = finishedAt.ToUniversalTime() - _startedAt;
            return new AuditRecord
            {
                Timestamp = _startedAt,
                CorrelationId = _correlationId,
                User = _user,
                Question = _question,
                StudentsConsulted = [.. _students],
                ResourcesConsulted = [.. _resources],
                Tools = [.. _tools],
                Citations = _answer is null ? [] : [.. _answer.Citations],
                Status = status,

                // R-LOG-10: motivo is null for a 200, whatever the answer carried.
                Reason = status == 200 ? null : _answer?.Reason,
                Tokens = _answer?.Tokens ?? 0,
                CostUsd = _answer?.CostUsd ?? 0m,
                DurationMs = Math.Max(0L, (long)Math.Round(elapsed.TotalMilliseconds, MidpointRounding.AwayFromZero)),
            };
        }
    }

    private static void AddDistinct(List<string> list, string value)
    {
        if (!list.Contains(value, StringComparer.Ordinal))
        {
            list.Add(value);
        }
    }
}
