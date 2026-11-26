using Xunit;
using TutorFlow.Infrastructure.Knowledge;

namespace TutorFlow.Infrastructure.Tests.Knowledge;

/// <summary>Spanish normalization of the search (R-FUE-01): ESC-CIT-06 and a battery of ESC-CIT questions.</summary>
public sealed class KnowledgeSearchRobustnessTests
{
    private const string Reglamento = "reglamento_academico_2025.md";
    private const string Evaluacion = "evaluacion_y_calificaciones.md";
    private const string Bienestar = "bienestar_y_becas.md";

    private static readonly KnowledgeBase Kb = KnowledgeBase.FromDirectory(null);

    [Theory]
    [InlineData("¿Quién puede consultar mis notas?", Reglamento, "Artículo 22")]
    [InlineData("¿Desde qué nota puedo habilitar una asignatura?", Reglamento, "Artículo 6")]
    [InlineData("¿Cuántas faltas puedo tener antes de perder una materia?", Reglamento, "Artículo 11")]
    [InlineData("¿Las faltas con excusa médica cuentan para la pérdida por inasistencia?", Reglamento, "Artículo 12")]
    [InlineData("¿Cuándo queda un estudiante en prueba académica?", Reglamento, "Artículo 20")]
    [InlineData("¿Cuándo se pierde el cupo?", Reglamento, "Artículo 21")]
    [InlineData("¿Sigue vigente el reglamento académico de 2023?", Reglamento, "Artículo 3")]
    [InlineData("Si pierdo una materia por faltas, ¿puedo habilitarla?", Reglamento, "Artículo 7")]
    [InlineData("¿En qué escala se califican las notas?", Evaluacion, "Sección 1")]
    [InlineData("¿Qué semanas comprende cada corte?", Evaluacion, "Sección 2")]
    [InlineData("¿Cuándo hay tutorías académicas?", Bienestar, "Sección 2")]
    [InlineData("¿De cuánto es el descuento de la beca socioeconómica?", Bienestar, "Sección 3.2")]
    [InlineData("¿Cuál es mi nota definitiva en Inglés III y cómo se calcula?", Evaluacion, "Sección 3")]
    public async Task Search_RetrievesTheExpectedSectionAmongTheTopFive(string question, string document, string section)
    {
        var results = await Kb.SearchAsync(question, 5, CancellationToken.None);

        Assert.Contains(results, r => r.Source.Document == document && r.Source.Section == section);
    }
}
