using System.Text.Json.Serialization;
using TutorFlow.Application.Assistant;

namespace TutorFlow.Api.Contracts;

/// <summary>A citation of the response (SPEC §5).</summary>
/// <param name="Document"><c>documento</c>.</param>
/// <param name="Section"><c>seccion</c>.</param>
public sealed record CitationDto(
    [property: JsonPropertyName("documento")] string Document,
    [property: JsonPropertyName("seccion")] string Section);

/// <summary>
/// Output of <c>POST /api/asistente/preguntar</c> (SPEC §5). Every status (200, 400, 403, 503) uses this same shape
/// (SPEC §5.0, D-08).
/// </summary>
/// <param name="Answer"><c>respuesta</c>.</param>
/// <param name="Citations"><c>citas</c>.</param>
/// <param name="UsedStudentData"><c>usoDatosEstudiante</c>.</param>
/// <param name="Tokens"><c>tokens</c>.</param>
/// <param name="CostUsd"><c>costoUsd</c>.</param>
public sealed record AskQuestionResponse(
    [property: JsonPropertyName("respuesta")] string Answer,
    [property: JsonPropertyName("citas")] IReadOnlyList<CitationDto> Citations,
    [property: JsonPropertyName("usoDatosEstudiante")] bool UsedStudentData,
    [property: JsonPropertyName("tokens")] int Tokens,
    [property: JsonPropertyName("costoUsd")] decimal CostUsd)
{
    /// <summary>Builds the response body from the use case result.</summary>
    /// <param name="answer">The use case result.</param>
    /// <returns>The response body.</returns>
    public static AskQuestionResponse From(AssistantAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return new AskQuestionResponse(
            answer.Text,
            [.. answer.Citations.Select(c => new CitationDto(c.Document, c.Section))],
            answer.UsedStudentData,
            answer.Tokens,
            answer.CostUsd);
    }
}
