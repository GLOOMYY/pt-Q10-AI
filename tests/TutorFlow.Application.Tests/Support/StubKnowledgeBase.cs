using TutorFlow.Application.Assistant;
using TutorFlow.Application.Ports.Knowledge;

namespace TutorFlow.Application.Tests.Support;

/// <summary>
/// Knowledge base double for the use case tests, with the real index of citable sections of escenarios/README §4.3
/// (R-FUE-02, CL-18). The real adapter (conocimiento) is tested in Infrastructure.Tests.
/// </summary>
public sealed class StubKnowledgeBase : IKnowledgeBase
{
    private static readonly HashSet<Citation> Index =
    [
        .. new[] { 1, 2, 3, 5, 6, 7, 10, 11, 12, 15, 16, 20, 21, 22 }
            .Select(n => new Citation("reglamento_academico_2025.md", $"Artículo {n}")),
        .. Enumerable.Range(1, 5).Select(n => new Citation("evaluacion_y_calificaciones.md", $"Sección {n}")),
        .. new[] { "Sección 1", "Sección 2", "Sección 3.1", "Sección 3.2" }
            .Select(s => new Citation("bienestar_y_becas.md", s)),
    ];

    public List<string> Queries { get; } = [];

    public bool IsCitable(Citation citation) => Index.Contains(citation);

    public Task<IReadOnlyList<KnowledgeFragment>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        Queries.Add(query);
        IReadOnlyList<KnowledgeFragment> fragments =
        [
            new(new Citation("reglamento_academico_2025.md", "Artículo 5"),
                "Una asignatura se aprueba con una nota definitiva igual o superior a 3.0."),
        ];
        return Task.FromResult(fragments);
    }
}
