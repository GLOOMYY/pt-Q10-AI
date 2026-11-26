using TutorFlow.Application.Assistant;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Tests.Support;

/// <summary>
/// Minimal recording <see cref="IRequestAuditContext"/> for the use case tests: it keeps what the use case, the tools
/// and the directory record, so the tests can check the log fields that the use case controls (R-LOG-04, R-LOG-06).
/// The real context and its in-memory log belong to seguridad.
/// </summary>
public sealed class RecordingAuditContext : IRequestAuditContext
{
    public List<string> Resources { get; } = [];

    public List<string> StudentsConsulted { get; } = [];

    public List<string> Tools { get; } = [];

    public List<AssistantAnswer> Completed { get; } = [];

    public string? Question { get; private set; }

    public AuditUser? User { get; private set; }

    public void Start(string correlationId, DateTimeOffset startedAt)
    {
    }

    public void SetRequest(string? question, AuditUser user)
    {
        Question = question;
        User = user;
    }

    public void RecordResource(string route)
    {
        if (!Resources.Contains(route))
        {
            Resources.Add(route);
        }
    }

    public void RecordStudentConsulted(StudentId studentId)
    {
        if (!StudentsConsulted.Contains(studentId.Value))
        {
            StudentsConsulted.Add(studentId.Value);
        }
    }

    public void RecordTool(string toolName) => Tools.Add(toolName);

    public void Complete(AssistantAnswer answer) => Completed.Add(answer);

    public AuditRecord BuildRecord(int status, DateTimeOffset finishedAt) => throw new NotSupportedException();
}
