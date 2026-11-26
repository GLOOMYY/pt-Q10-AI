using Xunit;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Audit;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Ports.Clock;
using TutorFlow.Testing.Audit;

namespace TutorFlow.Application.Tests.Security;

/// <summary>R-LOG-01..10, D-27, D-30, AC-03: the audit context, the recorder and the JSON line.</summary>
public sealed class AuditTrailTests
{
    private static readonly string[] ExactFields =
    [
        "timestamp", "correlationId", "usuario", "pregunta", "estudiantesConsultados", "recursosConsultados",
        "herramientas", "citas", "status", "motivo", "tokens", "costoUsd", "duracionMs",
    ];

    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private static readonly DateTimeOffset T0 = new(2026, 11, 26, 15, 0, 0, TimeSpan.Zero);

    private static StudentId Student(string id)
    {
        Assert.True(StudentId.TryParse(id, out var parsed));
        return parsed;
    }

    [Fact]
    public void Record_collects_what_really_happened_in_order_without_repeats_R_LOG_04_R_LOG_06()
    {
        var context = new RequestAuditContext();
        context.Start("abc", T0);
        context.SetRequest("  ¿Cuánto sacó Andrés?  ", new AuditUser("estudiante", "EST-001"));
        context.RecordResource("/programas");
        context.RecordResource("/estudiantes/EST-001");
        context.RecordResource("/programas");
        context.RecordStudentConsulted(Student("EST-001"));
        context.RecordStudentConsulted(Student("EST-001"));
        context.RecordTool("obtenerNotas");
        context.RecordTool("obtenerNotas");
        context.Complete(AssistantAnswer.Answered("secret text", [new Citation("a.md", "Sección 1")], true, 120, 0.0000125m));

        var record = context.BuildRecord(200, T0.AddMilliseconds(250));

        Assert.Equal("abc", record.CorrelationId);
        Assert.Equal(T0, record.Timestamp);
        Assert.Equal("  ¿Cuánto sacó Andrés?  ", record.Question); // D-27: as received
        Assert.Equal(new AuditUser("estudiante", "EST-001"), record.User);
        Assert.Equal(["/programas", "/estudiantes/EST-001"], record.ResourcesConsulted);
        Assert.Equal(["EST-001"], record.StudentsConsulted);
        Assert.Equal(["obtenerNotas", "obtenerNotas"], record.Tools);
        Assert.Equal([new Citation("a.md", "Sección 1")], record.Citations);
        Assert.Equal(200, record.Status);
        Assert.Null(record.Reason);
        Assert.Equal(120, record.Tokens);
        Assert.Equal(0.000013m, record.CostUsd);
        Assert.Equal(250, record.DurationMs);
    }

    [Fact]
    public void Forbidden_answer_keeps_its_reason_and_zero_tokens_R_LOG_10()
    {
        var context = new RequestAuditContext();
        context.Start("x", T0);
        context.SetRequest("q", new AuditUser("estudiante", "EST-001"));
        context.Complete(AssistantAnswer.Forbidden(AuditReasons.DeterministicBarrier));

        var record = context.BuildRecord(403, T0);

        Assert.Equal(403, record.Status);
        Assert.Equal("barrera determinista", record.Reason);
        Assert.Equal(0, record.Tokens);
        Assert.Equal(0m, record.CostUsd);
        Assert.Empty(record.StudentsConsulted);
        Assert.Equal([AssistantAnswer.ForbiddenCitation], record.Citations);
    }

    [Fact]
    public void Without_complete_the_record_is_empty_but_truthful()
    {
        var context = new RequestAuditContext();
        context.Start("x", T0);

        var record = context.BuildRecord(500, T0);

        Assert.Equal(500, record.Status);
        Assert.Null(record.Reason);
        Assert.Empty(record.Citations);
        Assert.Equal(0, record.Tokens);
        Assert.Null(record.Question);
        Assert.Null(record.User.Id);
    }

    [Fact]
    public void BuildRecord_before_Start_fails()
    {
        Assert.Throws<InvalidOperationException>(() => new RequestAuditContext().BuildRecord(200, T0));
    }

    [Fact]
    public async Task Recorder_writes_exactly_one_record_even_if_finished_twice_R_LOG_01()
    {
        var log = new InMemoryAuditLog();
        var clock = new TestClock(T0);
        var context = new RequestAuditContext();
        var recorder = new AuditTrailRecorder(context, log, clock, NullLogger<AuditTrailRecorder>.Instance);

        recorder.Begin("id-1");
        clock.UtcNow = T0.AddMilliseconds(40);
        await recorder.FinishAsync(403);
        await recorder.FinishAsync(403);

        var record = log.GetByCorrelationId("id-1");
        Assert.Single(log.Records);
        Assert.Equal(403, record.Status);
        Assert.Equal(40, record.DurationMs);
    }

    [Fact]
    public async Task Recorder_without_Begin_writes_nothing()
    {
        var log = new InMemoryAuditLog();
        var recorder = new AuditTrailRecorder(new RequestAuditContext(), log, new TestClock(T0), NullLogger<AuditTrailRecorder>.Instance);

        await recorder.FinishAsync(200);

        Assert.Empty(log.Records);
    }

    [Fact]
    public async Task A_failing_log_does_not_break_the_request()
    {
        var recorder = new AuditTrailRecorder(
            new RequestAuditContext(), new ThrowingLog(), new TestClock(T0), NullLogger<AuditTrailRecorder>.Instance);
        recorder.Begin("id");

        await recorder.FinishAsync(200);
    }

    private sealed class ThrowingLog : IAuditLog
    {
        public Task WriteAsync(AuditRecord record, CancellationToken cancellationToken) =>
            throw new IOException("disk full");
    }

    [Fact]
    public void Correlation_id_is_reused_when_reasonable_and_generated_otherwise_R_LOG_09()
    {
        Assert.Equal("eval-ESC-INY-01-7f3a", CorrelationId.Resolve("eval-ESC-INY-01-7f3a"));
        Assert.Equal("abc", CorrelationId.Resolve("  abc  "));

        var generated = CorrelationId.Resolve(null);
        Assert.False(string.IsNullOrWhiteSpace(generated));
        Assert.NotEqual(generated, CorrelationId.Resolve(""));

        foreach (var unreasonable in new[] { "has space", "line\nbreak", "ñandú", new string('a', CorrelationId.MaxLength + 1), "   " })
        {
            Assert.NotEqual(unreasonable, CorrelationId.Resolve(unreasonable));
        }
    }

    [Fact]
    public void Json_line_has_exactly_the_fields_of_R_LOG_08_and_no_escaped_non_ascii()
    {
        var context = new RequestAuditContext();
        context.Start("cid-1", T0);
        context.SetRequest("¿Cuánto sacó Andrés Martínez? \"x\"\nsegunda línea", new AuditUser("estudiante", "EST-001"));
        context.RecordResource("/programas");
        context.Complete(AssistantAnswer.Forbidden(AuditReasons.ThirdPartyData));
        var line = AuditJson.Serialize(context.BuildRecord(403, T0.AddMilliseconds(5)));

        Assert.DoesNotContain('\n', line);
        Assert.Contains("Andrés Martínez", line, StringComparison.Ordinal); // D-27, not é
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        Assert.Equal(ExactFields.Order(StringComparer.Ordinal), root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("2026-11-26T15:00:00.000Z", root.GetProperty("timestamp").GetString());
        Assert.Equal("estudiante", root.GetProperty("usuario").GetProperty("rol").GetString());
        Assert.Equal("EST-001", root.GetProperty("usuario").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("estudiantesConsultados").ValueKind);
        Assert.Equal("/programas", root.GetProperty("recursosConsultados")[0].GetString());
        Assert.Equal("Artículo 22", root.GetProperty("citas")[0].GetProperty("seccion").GetString());
        Assert.Equal("datos de terceros", root.GetProperty("motivo").GetString());
        Assert.Equal(0, root.GetProperty("tokens").GetInt32());
        Assert.Equal(5, root.GetProperty("duracionMs").GetInt64());
    }

    [Fact]
    public void Json_line_of_a_200_has_null_motivo()
    {
        var context = new RequestAuditContext();
        context.Start("cid", T0);
        context.Complete(AssistantAnswer.Answered("ok", [], false, 10, 0.5m));

        using var doc = JsonDocument.Parse(AuditJson.Serialize(context.BuildRecord(200, T0)));

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("motivo").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("pregunta").ValueKind);
    }

    [Fact]
    public void Record_never_contains_the_answer_text_or_names_R_LOG_02()
    {
        var context = new RequestAuditContext();
        context.Start("cid", T0);
        context.SetRequest("hola", new AuditUser("estudiante", "EST-001"));
        context.Complete(AssistantAnswer.Answered("Laura Gómez tiene 4.39", [], true, 1, 0m));

        var line = AuditJson.Serialize(context.BuildRecord(200, T0));

        Assert.DoesNotContain("Laura", line, StringComparison.Ordinal);
        Assert.DoesNotContain("4.39", line, StringComparison.Ordinal);
    }

    [Fact]
    public void InMemoryAuditLog_requires_exactly_one_record_per_correlation_id()
    {
        var log = new InMemoryAuditLog();
        Assert.Throws<InvalidOperationException>(() => log.GetByCorrelationId("none"));
    }
}
