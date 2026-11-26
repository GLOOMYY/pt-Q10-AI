using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Audit;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Infrastructure.Audit;
using Xunit;

namespace TutorFlow.Infrastructure.Tests.Audit;

/// <summary>R-LOG-02, R-LOG-05, R-LOG-07, R-LOG-08, D-27, D-30: the file and stdout adapter of the audit log.</summary>
public sealed class FileAuditLogTests : IDisposable
{
    private static readonly string[] ExactFields =
    [
        "citas", "correlationId", "costoUsd", "duracionMs", "estudiantesConsultados", "herramientas", "motivo",
        "pregunta", "recursosConsultados", "status", "timestamp", "tokens", "usuario",
    ];

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "tutorflow-audit-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string FilePath => Path.Combine(_directory, "var", "auditoria", "auditoria.jsonl");

    private FileAuditLog Create(StringWriter stdout) =>
        new(Options.Create(new AuditLogOptions { FilePath = FilePath }), stdout);

    private static AuditRecord Record(string id, string question = "¿Cuánto sacó Andrés Martínez?")
    {
        var context = new RequestAuditContext();
        context.Start(id, new DateTimeOffset(2026, 11, 26, 15, 0, 0, TimeSpan.Zero));
        context.SetRequest(question, new AuditUser("estudiante", "EST-001"));
        context.RecordResource("/programas");
        context.Complete(AssistantAnswer.Forbidden(AuditReasons.DeterministicBarrier));
        return context.BuildRecord(403, new DateTimeOffset(2026, 11, 26, 15, 0, 0, 12, TimeSpan.Zero));
    }

    [Fact]
    public async Task Writes_one_line_to_stdout_and_appends_it_to_the_file_creating_the_directory()
    {
        var stdout = new StringWriter();
        using var log = Create(stdout);

        await log.WriteAsync(Record("c1"), CancellationToken.None);
        await log.WriteAsync(Record("c2"), CancellationToken.None);

        var fileLines = (await File.ReadAllLinesAsync(FilePath, Encoding.UTF8)).ToArray();
        var stdoutLines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(2, fileLines.Length);
        Assert.Equal(fileLines, stdoutLines);
        Assert.Equal("c1", JsonDocument.Parse(fileLines[0]).RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("c2", JsonDocument.Parse(fileLines[1]).RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Line_has_exactly_the_fields_of_R_LOG_08_unescaped_and_no_BOM()
    {
        using var log = Create(new StringWriter());

        await log.WriteAsync(Record("c1"), CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(FilePath);
        Assert.NotEqual(0xEF, bytes[0]); // no UTF-8 BOM
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("Andrés Martínez", text, StringComparison.Ordinal); // D-27: the question as received, not escaped
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(text.TrimEnd('\n'));
        Assert.Equal(ExactFields, doc.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Concurrent_writes_never_interleave_lines()
    {
        var stdout = new StringWriter();
        using var log = Create(stdout);

        await Task.WhenAll(Enumerable.Range(0, 200).Select(i =>
            log.WriteAsync(Record("c" + i, "pregunta " + new string('x', 200)), CancellationToken.None)));

        var lines = await File.ReadAllLinesAsync(FilePath, Encoding.UTF8);
        Assert.Equal(200, lines.Length);
        var ids = lines.Select(l => JsonDocument.Parse(l).RootElement.GetProperty("correlationId").GetString()).ToHashSet();
        Assert.Equal(200, ids.Count);
    }

    [Fact]
    public async Task Log_never_holds_secrets_answers_or_prompts_R_LOG_05()
    {
        // The record type has no field for them; this checks the serialized line of a full flow.
        var context = new RequestAuditContext();
        context.Start("c", DateTimeOffset.UtcNow);
        context.SetRequest("hola", new AuditUser("estudiante", "EST-001"));
        context.Complete(AssistantAnswer.Answered("Laura Gómez tiene 4.39", [], true, 1, 0m));
        using var log = Create(new StringWriter());

        await log.WriteAsync(context.BuildRecord(200, DateTimeOffset.UtcNow), CancellationToken.None);

        var text = await File.ReadAllTextAsync(FilePath);
        Assert.DoesNotContain("Laura", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4.39", text, StringComparison.Ordinal);
    }
}
