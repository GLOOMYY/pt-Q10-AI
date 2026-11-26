using TutorFlow.Application.Assistant;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Api.Tests.Support;

/// <summary>
/// Minimal recording <see cref="IRequestAuditContext"/> for the endpoint tests: what the inbound adapter, the use case
/// and the tools record for one request. The real context and its in-memory log belong to seguridad.
/// </summary>
public sealed class RecordingAuditContext : IRequestAuditContext
{
    public string? Question { get; private set; }

    public AuditUser? User { get; private set; }

    public List<string> StudentsConsulted { get; } = [];

    public List<string> Resources { get; } = [];

    public List<string> Tools { get; } = [];

    public AssistantAnswer? Answer { get; private set; }

    public int CompleteCalls { get; private set; }

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

    public void Complete(AssistantAnswer answer)
    {
        Answer = answer;
        CompleteCalls++;
    }

    public AuditRecord BuildRecord(int status, DateTimeOffset finishedAt) => throw new NotSupportedException();
}
