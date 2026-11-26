using System.Text.Json.Serialization;

namespace TutorFlow.Application.Assistant;

/// <summary>
/// A citation <c>{ documento, seccion }</c> (R-RES-01, R-RES-02). The JSON names are the exact ones of the
/// contract (§5) and of the audit record (R-LOG-08).
/// </summary>
/// <param name="Document">Document file name (e.g. <c>reglamento_academico_2025.md</c>) or data source
/// (<c>Datos del estudiante</c>, <c>Datos del programa</c>).</param>
/// <param name="Section">Section, e.g. <c>Artículo 22</c>, <c>Sección 3.1</c> or <c>Notas · periodo 2026-2</c>.</param>
public sealed record Citation(
    [property: JsonPropertyName("documento")] string Document,
    [property: JsonPropertyName("seccion")] string Section);
