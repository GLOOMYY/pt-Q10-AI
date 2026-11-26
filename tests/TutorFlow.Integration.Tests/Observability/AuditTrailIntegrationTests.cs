using System.Net;
using System.Text;
using System.Text.Json;
using TutorFlow.Integration.Tests.Scripts;
using TutorFlow.Integration.Tests.Support;
using Xunit;

namespace TutorFlow.Integration.Tests.Observability;

/// <summary>
/// AC-03, R-LOG-01, R-LOG-02, R-LOG-05, R-LOG-07..09, D-30 with the real <c>Program.cs</c> pipeline: the correlation id,
/// one audit record per request with the real status, and nothing sensitive in the record.
/// </summary>
public sealed class AuditTrailIntegrationTests
{
    private const string Route = "/api/asistente/preguntar";
    private const string FakeKey = "integration-tests-fake-key";

    // R-LOG-08: exact JSON field names of the audit record.
    private static readonly string[] RecordFields =
    [
        "citas", "correlationId", "costoUsd", "duracionMs", "estudiantesConsultados", "herramientas", "motivo",
        "pregunta", "recursosConsultados", "status", "timestamp", "tokens", "usuario",
    ];

    private static readonly JsonSerializerOptions Unescaped = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static HttpRequestMessage Post(string body, string? correlationId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (correlationId is not null)
        {
            request.Headers.Add(IntegrationHost.CorrelationHeader, correlationId);
        }

        return request;
    }

    private static string Question(string text, string role = "estudiante", string id = "EST-001") =>
        JsonSerializer.Serialize(new { pregunta = text, usuario = new { rol = role, id } });

    [Fact]
    public async Task Health_always_carries_a_correlation_id_and_reuses_the_one_received()
    {
        await using var host = new IntegrationHost();
        using var client = host.CreateClient();

        using var generated = await client.GetAsync(new Uri("/api/salud", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(generated.Headers.GetValues(IntegrationHost.CorrelationHeader).Single()));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/salud");
        request.Headers.Add(IntegrationHost.CorrelationHeader, "caller-supplied-id-1");
        using var reused = await client.SendAsync(request);
        Assert.Equal("caller-supplied-id-1", reused.Headers.GetValues(IntegrationHost.CorrelationHeader).Single());
    }

    [Fact]
    public async Task The_assistant_response_reuses_the_correlation_id_and_the_record_carries_it()
    {
        await using var host = new IntegrationHost();
        host.Llm.EnqueueToolCalls(Tool.Search("nota mínima")).EnqueueText(
            """{"respuesta":"La nota mínima es 3.0","citas":[{"documento":"reglamento_academico_2025.md","seccion":"Artículo 5"}]}""");
        using var client = host.CreateClient();

        using var response = await client.SendAsync(Post(Question("¿Cuál es la nota mínima para aprobar?"), "evals-run-1-ESC-X-01"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("evals-run-1-ESC-X-01", response.Headers.GetValues(IntegrationHost.CorrelationHeader).Single());
        var record = host.Audit.GetByCorrelationId("evals-run-1-ESC-X-01");
        Assert.Equal(200, record.Status);
        Assert.Null(record.Reason);
    }

    [Fact]
    public async Task A_missing_or_unusable_correlation_id_is_replaced_by_a_generated_one_with_its_record()
    {
        await using var host = new IntegrationHost();
        using var client = host.CreateClient();

        using var withoutHeader = await client.SendAsync(Post("{}"));
        var generated = withoutHeader.Headers.GetValues(IntegrationHost.CorrelationHeader).Single();
        Assert.False(string.IsNullOrWhiteSpace(generated));
        Assert.Equal(generated, host.Audit.GetByCorrelationId(generated).CorrelationId);

        using var unusable = await client.SendAsync(Post("{}", new string('a', 129)));
        var replaced = unusable.Headers.GetValues(IntegrationHost.CorrelationHeader).Single();
        Assert.NotEqual(new string('a', 129), replaced);
        Assert.Single(host.Audit.FindAll(replaced));
    }

    [Theory]
    [InlineData("{", 400, "solicitud inválida")]
    [InlineData("""{"pregunta":"¿Cuál es mi promedio?","usuario":{"rol":"estudiante","id":"EST-999"}}""", 403, "identidad inválida")]
    [InlineData("""{"pregunta":"¿Cuál es la nota de Andrés Martínez?","usuario":{"rol":"estudiante","id":"EST-001"}}""", 403, "barrera determinista")]
    public async Task Each_request_leaves_exactly_one_record_with_the_status_and_reason_really_returned(string body, int status, string reason)
    {
        await using var host = new IntegrationHost();
        using var client = host.CreateClient();

        using var first = await client.SendAsync(Post(body, "audit-one"));
        using var second = await client.SendAsync(Post(body, "audit-two"));

        Assert.Equal(status, (int)first.StatusCode);
        Assert.Equal(2, host.Audit.Records.Count);
        foreach (var id in new[] { "audit-one", "audit-two" })
        {
            var record = host.Audit.GetByCorrelationId(id);
            Assert.Equal(status, record.Status);
            Assert.Equal(reason, record.Reason);
            Assert.Empty(record.StudentsConsulted);
        }
    }

    [Fact]
    public async Task The_status_of_a_503_is_the_real_one_and_the_audit_keeps_the_dependency_reason()
    {
        await using var host = new IntegrationHost();
        host.Api.Stop();
        using var client = host.CreateClient();

        using var response = await client.SendAsync(Post(Question("¿Cuál es mi promedio?"), "audit-503"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var record = host.Audit.GetByCorrelationId("audit-503");
        Assert.Equal(503, record.Status);
        Assert.Equal("dependencia no disponible", record.Reason);
    }

    [Fact]
    public async Task The_record_has_the_fields_of_R_LOG_08_and_nothing_sensitive()
    {
        const string answer = "Laura Gómez, tu promedio ponderado es 4.39 y tu nota en Bases de Datos es 4.1.";
        await using var host = new IntegrationHost();
        host.Llm.EnqueueToolCalls(Tool.Status()).EnqueueText(
            JsonSerializer.Serialize(new { respuesta = answer, citas = Array.Empty<object>() }));
        using var client = host.CreateClient();

        using var response = await client.SendAsync(Post(Question("¿Cuál es mi promedio ponderado del periodo?"), "audit-sensitive"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = host.Audit.GetByCorrelationId("audit-sensitive");
        var json = JsonSerializer.SerializeToElement(record);
        var fields = json.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(
            RecordFields.Order(StringComparer.Ordinal),
            fields);

        Assert.Equal(["EST-001"], record.StudentsConsulted);
        Assert.Contains("consultar_situacion_academica", record.Tools);
        Assert.True(record.Tokens > 0);

        // R-LOG-02, R-LOG-05: no grades, no student names, no answer text, no secrets, no prompts (the question is kept as is, D-27).
        // The timestamp and the numeric metrics (tokens, cost, duration) are machine-generated values that change on every
        // run and can contain a grade-like digit sequence by chance (e.g. "...:04.1634539"): they are not text that can
        // leak content, so only the textual fields are scanned. Every textual field except the question is still checked.
        var withoutQuestion = JsonSerializer.Serialize(
            json.EnumerateObject()
                .Where(p => p.Name is not ("pregunta" or "timestamp") && p.Value.ValueKind != JsonValueKind.Number)
                .ToDictionary(p => p.Name, p => p.Value),
            Unescaped);
        foreach (var forbidden in new[] { "4.39", "4.1", "Laura", "Gómez", answer, FakeKey, "LLM_API_KEY", "Eres el asistente", "system" })
        {
            Assert.DoesNotContain(forbidden, withoutQuestion, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task The_audit_log_is_never_exposed_by_an_http_endpoint()
    {
        await using var host = new IntegrationHost();
        using var client = host.CreateClient();
        using var _ = await client.SendAsync(Post(Question("¿Cuál es mi promedio?", id: "EST-999"), "audit-probe"));

        foreach (var route in new[] { "/api/auditoria", "/api/log", "/api/logs", "/auditoria", "/var/auditoria/auditoria.jsonl", "/api/asistente/auditoria" })
        {
            using var response = await client.GetAsync(new Uri(route, UriKind.Relative));
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"{route} answered {(int)response.StatusCode}");
            Assert.DoesNotContain("audit-probe", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }
}
