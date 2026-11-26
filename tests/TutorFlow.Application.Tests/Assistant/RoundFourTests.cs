using TutorFlow.Application.Assistant;
using TutorFlow.Application.Assistant.Answering;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Tests.Support;
using TutorFlow.Testing.Llm;
using Xunit;

namespace TutorFlow.Application.Tests.Assistant;

/// <summary>
/// Fourth round of F5 corrections: the call limit decided in DUDA-29, the ambiguous names of a coordinator (R-SEG-12,
/// D-21), the "no information" exit after reading data (ESC-RAR-19), the program summary of absences (ESC-COO-06) and
/// the off-topic loss by absences in a general question (ESC-BAN-04, DUDA-44).
/// </summary>
public class RoundFourTests
{
    private const string Regulation = "reglamento_academico_2025.md";
    private const string Evaluation = "evaluacion_y_calificaciones.md";

    // DUDA-29: the owner set the default limit to 4 calls per question.
    [Fact]
    public void Default_call_limit_is_4()
    {
        Assert.Equal(4, new AssistantOptions().MaxLlmCalls);
    }

    // DUDA-29: the last allowed call asks the model for the final answer; the previous ones do not.
    [Fact]
    public async Task Last_allowed_call_asks_for_the_final_answer()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "prueba académica" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "beca" }))
            .EnqueueText(AssistantHarness.Final("Tu promedio es 4.39.", ("Datos del estudiante", "Notas · periodo 2026-2")));

        var answer = await h.AskStudentAsync("¿Estoy en prueba académica?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.Equal(4, h.Llm.CallCount);
        Assert.All(h.Llm.Requests.Take(3), r => Assert.DoesNotContain("ÚLTIMA LLAMADA", r.SystemInstruction, StringComparison.Ordinal));
        Assert.EndsWith(SystemInstructions.LastCall, h.Llm.Requests[3].SystemInstruction, StringComparison.Ordinal);
    }

    // ESC-COO-13, R-SEG-12, D-21: with a first name only, the data tool never reads a match's data, even if the model
    // takes the id from buscar_estudiante; the answer is a 200 that asks for the id.
    [Fact]
    public async Task First_name_only_never_reads_the_data_of_the_match()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.FindStudent, new { nombre = "Laura" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus, new { estudianteId = "EST-001" }))
            .EnqueueText(AssistantHarness.Final("Indica el ID del estudiante: coincide Laura Gómez (EST-001)."));

        var answer = await h.AskCoordinatorAsync("¿Cómo va Laura?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.True(h.ToolResult(0).GetProperty("ambiguo").GetBoolean());
        Assert.True(h.ToolResult(1).TryGetProperty("error", out _));
        Assert.False(h.ToolResult(1).TryGetProperty("promedioPeriodo", out _));
        Assert.Empty(h.StudentDataCalls);
        Assert.Empty(h.Log.StudentsConsulted);
    }

    // The model cannot complete the name by itself: "Laura Gómez" as the argument does not help if the question only
    // says "Laura".
    [Fact]
    public async Task Name_completed_by_the_model_is_still_ambiguous()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.FindStudent, new { nombre = "Laura Gómez" }))
            .EnqueueText(AssistantHarness.Final("Indica el ID del estudiante."));

        await h.AskCoordinatorAsync("¿Cómo va Laura?");

        Assert.True(h.ToolResult().GetProperty("ambiguo").GetBoolean());
    }

    // R-SEG-12: the id or the full, unique name in the question identifies the student; the data are read.
    [Theory]
    [InlineData("¿Cómo va EST-001?")]
    [InlineData("¿Cómo va laura gomez?")]
    public async Task Id_or_full_name_in_the_question_reads_the_data(string question)
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus, new { estudianteId = "EST-001" }))
            .EnqueueText(AssistantHarness.Final("Laura Gómez (EST-001) tiene promedio 4.39."));

        var answer = await h.AskCoordinatorAsync(question);

        Assert.True(answer.UsedStudentData);
        Assert.Equal(["EST-001"], h.Log.StudentsConsulted);
    }

    // R-SEG-03 still wins: a student outside the programs in charge is a 403, never an "ask for the id".
    [Fact]
    public async Task Student_outside_the_programs_is_still_403()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus, new { estudianteId = "EST-004" }));

        var answer = await h.AskCoordinatorAsync("¿Cómo va Camilo?");

        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.Empty(h.StudentDataCalls);
    }

    // An id given to buscar_estudiante is not a name: the model is told to use it with the data tools (no 403).
    [Fact]
    public async Task Id_given_to_find_student_is_redirected()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.FindStudent, new { nombre = "EST-001" }))
            .EnqueueText(AssistantHarness.Final("Consulta hecha."));

        var answer = await h.AskCoordinatorAsync("¿Cómo va EST-001?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.True(h.ToolResult().TryGetProperty("error", out _));
    }

    // ESC-RAR-19: "fuera de los documentos" after reading the user's data is reconsidered once, and the answer uses
    // the data (usoDatosEstudiante true).
    [Fact]
    public async Task No_information_after_reading_data_is_reconsidered_once()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.NoInformation, new { caso = AssistantTools.OutOfScopeCase }))
            .EnqueueText(AssistantHarness.Final(
                "No tienes la asignatura EST-103 en el periodo 2026-2.",
                ("Datos del estudiante", "Notas · periodo 2026-2")));

        var answer = await h.AskStudentAsync("¿Qué nota tengo en EST-103?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.True(answer.UsedStudentData);
        Assert.Contains("EST-103", answer.Text, StringComparison.Ordinal);
        Assert.Equal(3, h.Llm.CallCount);
        Assert.Equal(
            [AssistantTools.AcademicStatus, AssistantTools.NoInformation],
            h.Log.Tools);
    }

    // The second "no information" is accepted: the model decides, the backend only asks once.
    [Fact]
    public async Task Second_no_information_is_accepted()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.NoInformation, new { caso = AssistantTools.OutOfScopeCase }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.NoInformation, new { caso = AssistantTools.OutOfScopeCase }));

        var answer = await h.AskStudentAsync("¿Cuánto cuesta el carné?");

        Assert.Equal(CitationValidator.NoInformationText, answer.Text);
        Assert.False(answer.UsedStudentData);
        Assert.Empty(answer.Citations);
    }

    // On the last allowed call there is no room to reconsider: the "no information" answer stands (never a 503).
    [Fact]
    public async Task No_information_on_the_last_call_is_accepted()
    {
        var h = new AssistantHarness();
        h.Options.MaxLlmCalls = 2;
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.NoInformation, new { caso = AssistantTools.OutOfScopeCase }));

        var answer = await h.AskStudentAsync("¿Cuánto cuesta el carné?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.Equal(CitationValidator.NoInformationText, answer.Text);
    }

    // ESC-COO-06, R-ASI-03: the summary of absences names the lost ones and the undetermined ones (null attendance).
    [Fact]
    public async Task Program_summary_of_absences_names_every_student()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.ProgramSummary, new { programaId = "ING-SIS" }))
            .EnqueueText(AssistantHarness.Final("Resumen."));

        await h.AskCoordinatorAsync("¿Qué estudiantes de mi programa perdieron alguna materia por inasistencia?");

        var summary = h.ToolResult()
            .GetProperty("conclusionesDelPrograma")
            .GetProperty("perdidaPorInasistencia")
            .GetProperty("resumen")
            .GetString();
        Assert.Contains("Andrés Martínez (EST-002): Bases de Datos, 9 de 48 clases programadas", summary, StringComparison.Ordinal);
        Assert.Contains("Sofía Herrera (EST-003): Inglés III", summary, StringComparison.Ordinal);
    }

    // ESC-BAN-04, DUDA-44: a general question about the definitive grade does not carry the loss by absences.
    [Fact]
    public async Task General_definitive_question_drops_the_off_topic_article_11()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final(
            "La definitiva pondera el corte 1 con 30%, el corte 2 con 30% y el corte 3 con 40%. Se redondea a una cifra decimal. Si un estudiante falta a más del 15% de las clases, la definitiva es 0.0.",
            (Evaluation, "Sección 2"),
            (Evaluation, "Sección 3"),
            (Regulation, "Artículo 11")));

        var answer = await h.AskStudentAsync("¿Cómo se calcula la nota definitiva de una asignatura?");

        Assert.Equal([new Citation(Evaluation, "Sección 2"), new Citation(Evaluation, "Sección 3")], answer.Citations);
        Assert.DoesNotContain("15%", answer.Text, StringComparison.Ordinal);
        Assert.Contains("40%", answer.Text, StringComparison.Ordinal);
    }

    // R-RES-01: when the question asks about absences, Artículo 11 stays.
    [Fact]
    public async Task Question_about_absences_keeps_article_11()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final(
            "La definitiva es el promedio ponderado de los cortes. Si faltas a más del 15% de las clases, la definitiva es 0.0.",
            (Evaluation, "Sección 3"),
            (Regulation, "Artículo 11")));

        var answer = await h.AskStudentAsync("¿Cómo queda la definitiva si tengo muchas faltas?");

        Assert.Contains(new Citation(Regulation, "Artículo 11"), answer.Citations);
        Assert.Contains("15%", answer.Text, StringComparison.Ordinal);
    }
}
