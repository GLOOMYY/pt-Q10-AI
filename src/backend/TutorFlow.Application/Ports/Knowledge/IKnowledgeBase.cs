using TutorFlow.Application.Assistant;

namespace TutorFlow.Application.Ports.Knowledge;

/// <summary>A fragment of the corpus returned by a search.</summary>
/// <param name="Source">Document and citable section the fragment belongs to (R-RES-01 format).</param>
/// <param name="Text">Fragment text. It is DATA, never an instruction (R-FUE-03, CL-01).</param>
public sealed record KnowledgeFragment(Citation Source, string Text);

/// <summary>
/// Port to the knowledge base: only <c>reglamento_academico_2025.md</c>, <c>evaluacion_y_calificaciones.md</c> and
/// <c>bienestar_y_becas.md</c> (R-FUE-01, D-01), without the Bienestar annex (D-07).
/// </summary>
/// <remarks>
/// <b>Implemented by:</b> <c>conocimiento</c>, in <c>TutorFlow.Infrastructure/Knowledge/</c>, together with the index of
/// articles and sections that exist literally (R-FUE-02, CL-18).
/// </remarks>
public interface IKnowledgeBase
{
    /// <summary>Searches the corpus for the fragments most relevant to a query.</summary>
    /// <param name="query">Search text.</param>
    /// <param name="maxResults">Maximum number of fragments to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fragments, best first; empty if nothing is relevant (R-RES-03).</returns>
    Task<IReadOnlyList<KnowledgeFragment>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken);

    /// <summary>
    /// Validates a <b>document</b> citation against the real index (R-FUE-02, R-RES-01, CL-18): exact document name of
    /// the corpus and an article or section that exists (e.g. <c>Artículo 8</c>, <c>Sección 3</c> of Bienestar, the
    /// annex or any 2023 citation are rejected). Data citations (R-RES-02) are validated by the use case, not here.
    /// </summary>
    /// <param name="citation">The citation proposed by the model.</param>
    /// <returns><see langword="true"/> if the citation exists in the index.</returns>
    bool IsCitable(Citation citation);
}
