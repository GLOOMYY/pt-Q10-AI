using TutorFlow.Application.Assistant;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Tests.Support;
using TutorFlow.Testing.Llm;
using Xunit;

namespace TutorFlow.Application.Tests.Assistant;

/// <summary>
/// Fifth round of F5 corrections: the off-topic "Artículo 7" in a general question about habilitation (ESC-CIT-01) and
/// the rule of the performance scholarship when the answer applies it to consulted data (ESC-COO-04, R-RES-02).
/// </summary>
public class RoundFiveTests
{
    private const string Regulation = "reglamento_academico_2025.md";
    private const string Welfare = "bienestar_y_becas.md";

    // ESC-CIT-01, R-RES-01: a general question about the habilitation grade keeps only Artículo 6; R-ARQ-03: the text
    // written by the model is returned untouched.
    [Fact]
    public async Task General_habilitation_question_drops_the_off_topic_article_7_and_keeps_the_text()
    {
        const string text = "Puedes habilitar una asignatura si la repruebas con una definitiva igual o superior a 2.0. Las prácticas profesionales no son habilitables.";
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final(text, (Regulation, "Artículo 6"), (Regulation, "Artículo 7")));

        var answer = await h.AskStudentAsync("¿Desde qué nota puedo habilitar una asignatura?");

        Assert.Equal([new Citation(Regulation, "Artículo 6")], answer.Citations);
        Assert.Equal(text, answer.Text);
    }

    // R-RES-01: a question about the courses that cannot be habilitated keeps Artículo 7.
    [Theory]
    [InlineData("¿Qué asignaturas no son habilitables?")]
    [InlineData("¿Puedo habilitar la práctica profesional?")]
    [InlineData("¿Se puede habilitar una materia perdida por inasistencia?")]
    public async Task Question_about_non_habilitable_courses_keeps_article_7(string question)
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final(
            "Se habilita con una definitiva entre 2.0 y 3.0; las prácticas, los trabajos de grado y las perdidas por inasistencia no son habilitables.",
            (Regulation, "Artículo 6"),
            (Regulation, "Artículo 7")));

        var answer = await h.AskStudentAsync(question);

        Assert.Contains(new Citation(Regulation, "Artículo 7"), answer.Citations);
    }

    // ESC-COO-04, R-BEC-01: a program answer about the performance scholarship cites Sección 3.1 even if the model
    // forgot it.
    [Fact]
    public async Task Performance_scholarship_in_a_program_answer_adds_section_3_1()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.ProgramSummary, new { programaId = "ING-SIS" }))
            .EnqueueText(AssistantHarness.Final(
                "Laura Gómez (EST-001) cumple los requisitos de la beca por rendimiento, con promedio 4.39.",
                ("Datos del programa", "ING-SIS · periodo 2026-2")));

        var answer = await h.AskCoordinatorAsync("¿Qué estudiantes de mi programa cumplen los requisitos de la beca por rendimiento?");

        Assert.Equal(
            [
                new Citation("Datos del programa", "ING-SIS · periodo 2026-2"),
                new Citation(Welfare, "Sección 3.1"),
            ],
            answer.Citations);
    }

    // R-RES-01: the same data with an answer that does not talk about the scholarship gets no extra citation.
    [Fact]
    public async Task Section_3_1_is_not_added_when_the_answer_does_not_use_it()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final("Tu promedio del periodo es 4.39.", ("Datos del estudiante", "Notas · periodo 2026-2")));

        var answer = await h.AskStudentAsync("¿Cuál es mi promedio?");

        Assert.DoesNotContain(new Citation(Welfare, "Sección 3.1"), answer.Citations);
    }

    // R-RES-01: without consulted data nothing backs Sección 3.1, whatever the answer says.
    [Fact]
    public async Task Section_3_1_is_not_added_without_consulted_data()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final("La beca por rendimiento exige un promedio alto.", (Regulation, "Artículo 5")));

        var answer = await h.AskStudentAsync("¿Qué es la beca por rendimiento?");

        Assert.Equal([new Citation(Regulation, "Artículo 5")], answer.Citations);
    }
}
