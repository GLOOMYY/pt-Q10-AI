using TutorFlow.Application.Assistant;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Tests.Support;
using TutorFlow.Testing.Llm;
using Xunit;

namespace TutorFlow.Application.Tests.Assistant;

/// <summary>
/// Citations the backend completes deterministically after the model answers (R-ARQ-03): the rule that the consulted
/// data really involve (R-RES-02: data + rule) and the weights of the cuts behind the definitive grade (R-RES-01,
/// R-CAL-01). Nothing is added without its support.
/// </summary>
public class CitationCompletionTests
{
    private const string Regulation = "reglamento_academico_2025.md";
    private const string Evaluation = "evaluacion_y_calificaciones.md";

    // ESC-COO-01: a program answer that names a loss by absences cites Artículo 11 even if the model forgot it.
    [Fact]
    public async Task Loss_by_absences_in_the_data_adds_article_11_when_the_answer_uses_it()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.ProgramSummary, new { programaId = "ING-SIS" }))
            .EnqueueText(AssistantHarness.Final(
                "Andrés Martínez perdió Cálculo Integral (2.6) y Bases de Datos por inasistencia (0.0).",
                ("Datos del programa", "ING-SIS · periodo 2026-2"),
                (Regulation, "Artículo 5")));

        var answer = await h.AskCoordinatorAsync("¿Qué estudiantes de mi programa tienen alguna asignatura perdida?");

        Assert.Equal(
            [
                new Citation("Datos del programa", "ING-SIS · periodo 2026-2"),
                new Citation(Regulation, "Artículo 5"),
                new Citation(Regulation, "Artículo 11"),
            ],
            answer.Citations);
    }

    // R-RES-01: the same data with an answer that does not talk about absences gets no extra citation.
    [Fact]
    public async Task Article_11_is_not_added_when_the_answer_does_not_use_it()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final("Puedes habilitar Cálculo Integral (2.6).", ("Datos del estudiante", "Notas · periodo 2026-2")));

        var answer = await h.AskStudentAsync("¿Qué materias puedo habilitar?", "EST-002");

        Assert.DoesNotContain(new Citation(Regulation, "Artículo 11"), answer.Citations);
    }

    // R-RES-01: without consulted data nothing backs Artículo 11, whatever the answer says.
    [Fact]
    public async Task Article_11_is_not_added_without_data_that_involve_it()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final("Faltar más del 15 % causa pérdida por inasistencia.", (Regulation, "Artículo 10")));

        var answer = await h.AskStudentAsync("¿La asistencia es obligatoria?");

        Assert.Equal([new Citation(Regulation, "Artículo 10")], answer.Citations);
    }

    // ESC-BAN-04, R-CAL-01: explaining the weights of the cuts with Sección 3 also cites Sección 2, in document order.
    [Fact]
    public async Task Weights_of_the_cuts_add_section_2_before_section_3()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final(
            "La definitiva pondera el corte 1 con 30%, el corte 2 con 30 % y el corte 3 con 40%, redondeada a un decimal.",
            (Evaluation, "Sección 3")));

        var answer = await h.AskStudentAsync("¿Cómo se calcula la nota definitiva de una asignatura?");

        Assert.Equal([new Citation(Evaluation, "Sección 2"), new Citation(Evaluation, "Sección 3")], answer.Citations);
    }

    // R-RES-01: Sección 3 without the weights in the answer stays alone.
    [Fact]
    public async Task Section_3_without_the_weights_stays_alone()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final("La definitiva se redondea a una cifra decimal.", (Evaluation, "Sección 3")));

        var answer = await h.AskStudentAsync("¿Cómo se redondea la definitiva?");

        Assert.Equal([new Citation(Evaluation, "Sección 3")], answer.Citations);
    }
}
