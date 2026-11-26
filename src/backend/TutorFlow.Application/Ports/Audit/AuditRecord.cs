using System.Text.Json.Serialization;
using TutorFlow.Application.Assistant;

namespace TutorFlow.Application.Ports.Audit;

/// <summary>The user as declared in the request body (<c>usuario</c>), for the audit record.</summary>
/// <param name="Role">Field <c>rol</c>; <see langword="null"/> if the body did not carry it (e.g. a 400).</param>
/// <param name="Id">Field <c>id</c>; <see langword="null"/> if the body did not carry it (e.g. a 400).</param>
public sealed record AuditUser(
    [property: JsonPropertyName("rol")] string? Role,
    [property: JsonPropertyName("id")] string? Id);

/// <summary>
/// Exactly one structured record per request to <c>POST /api/asistente/preguntar</c> (R-LOG-01), with the exact JSON
/// field names of R-LOG-08. It never holds grades, absences, averages, student names, the answer text, secrets or
/// prompts (R-LOG-02, R-LOG-05); the question is kept as received (D-27).
/// </summary>
public sealed record AuditRecord
{
    /// <summary><c>timestamp</c>: date and time in UTC.</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary><c>correlationId</c>: the value of the <c>X-Correlation-Id</c> header of the response (R-LOG-09).</summary>
    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    /// <summary><c>usuario</c>: role and id as declared in the body.</summary>
    [JsonPropertyName("usuario")]
    public required AuditUser User { get; init; }

    /// <summary><c>pregunta</c>: question text as received (D-27); <see langword="null"/> if the body had none.</summary>
    [JsonPropertyName("pregunta")]
    public required string? Question { get; init; }

    /// <summary>
    /// <c>estudiantesConsultados</c>: ids whose <c>/estudiantes/{id}</c>, <c>/notas</c> or <c>/asistencia</c> were
    /// actually requested (R-LOG-04, R-LOG-06). Listing a program does not count (D-24).
    /// </summary>
    [JsonPropertyName("estudiantesConsultados")]
    public required IReadOnlyList<string> StudentsConsulted { get; init; }

    /// <summary><c>recursosConsultados</c>: routes requested to the simulated API (see <c>AcademicApiRoutes</c>).</summary>
    [JsonPropertyName("recursosConsultados")]
    public required IReadOnlyList<string> ResourcesConsulted { get; init; }

    /// <summary><c>herramientas</c>: names of the tools the model invoked, in order.</summary>
    [JsonPropertyName("herramientas")]
    public required IReadOnlyList<string> Tools { get; init; }

    /// <summary><c>citas</c>: citations returned to the user.</summary>
    [JsonPropertyName("citas")]
    public required IReadOnlyList<Citation> Citations { get; init; }

    /// <summary><c>status</c>: HTTP status code returned.</summary>
    [JsonPropertyName("status")]
    public required int Status { get; init; }

    /// <summary><c>motivo</c>: one of <see cref="AuditReasons"/>; <see langword="null"/> when the status is 200 (R-LOG-10).</summary>
    [JsonPropertyName("motivo")]
    public required string? Reason { get; init; }

    /// <summary><c>tokens</c>: same value as the response (R-RES-05).</summary>
    [JsonPropertyName("tokens")]
    public required int Tokens { get; init; }

    /// <summary><c>costoUsd</c>: same value as the response (R-RES-05).</summary>
    [JsonPropertyName("costoUsd")]
    public required decimal CostUsd { get; init; }

    /// <summary><c>duracionMs</c>: request duration in milliseconds.</summary>
    [JsonPropertyName("duracionMs")]
    public required long DurationMs { get; init; }
}
