using TutorFlow.Application.Assistant;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Tests.Support;
using TutorFlow.Testing.Llm;
using Xunit;

namespace TutorFlow.Application.Tests.Assistant;

/// <summary>
/// Sixth round of F5 corrections: the last allowed call offers no data tool, so a flow with three rounds of tools ends
/// with the model's answer instead of a 503 (DUDA-29; ESC-COO-08, ESC-COO-09, ESC-INY-14).
/// </summary>
public class RoundSixTests
{
    private const string Regulation = "reglamento_academico_2025.md";

    private static readonly string[] Exits = [AssistantTools.Reject, AssistantTools.NoInformation];

    // DUDA-29, ESC-COO-08, ESC-COO-09: three rounds of tools, then the fourth (last) call answers: 200, and that call
    // offers only the exits, never a data tool nor buscar_reglamento.
    [Fact]
    public async Task Three_tool_rounds_end_with_the_answer_of_the_fourth_call()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.FindStudent, new { nombre = "Laura Gómez" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus, new { estudianteId = "EST-001" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "prueba académica" }))
            .EnqueueText(AssistantHarness.Final(
                "Laura Gómez (EST-001) no está en prueba académica.",
                ("Datos del programa", "ING-SIS · periodo 2026-2"),
                (Regulation, "Artículo 20")));

        var answer = await h.AskCoordinatorAsync("¿Laura Gómez está en prueba académica?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.Equal(4, h.Llm.CallCount);
        Assert.True(answer.UsedStudentData);
        Assert.Contains(new Citation(Regulation, "Artículo 20"), answer.Citations);
        Assert.Equal(Exits, h.Llm.Requests[3].Tools.Select(t => t.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.All(h.Llm.Requests.Take(3), r => Assert.Contains(r.Tools, t => t.Name == AssistantTools.AcademicStatus));
    }

    // The student's last call offers the same two exits, with their original definitions.
    [Fact]
    public async Task Last_call_of_a_student_offers_only_the_exits()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "promedio" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "definitiva" }))
            .EnqueueText(AssistantHarness.Final("Tu promedio es 4.39.", ("Datos del estudiante", "Notas · periodo 2026-2")));

        var answer = await h.AskStudentAsync("¿Cuál es mi promedio?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        var lastTools = h.Llm.Requests[3].Tools;
        Assert.Equal(2, lastTools.Count);
        Assert.All(lastTools, t => Assert.Contains(t, AssistantTools.StudentDefinitions));
        Assert.DoesNotContain(lastTools, t => t.Name == AssistantTools.SearchRegulations);
    }

    // ESC-INY-14, ESC-RAR-19: search → data → "fuera de los documentos" (reconsidered once) → the last call, without
    // data tools, answers with the data already read: 200, never a 503.
    [Fact]
    public async Task Reconsidered_no_information_before_the_last_call_ends_with_an_answer()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "reglamento 2023" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.NoInformation, new { caso = AssistantTools.OutOfScopeCase }))
            .EnqueueText(AssistantHarness.Final(
                "Con el reglamento vigente perdiste Bases de Datos por inasistencia.",
                ("Datos del estudiante", "Asistencia · periodo 2026-2"),
                (Regulation, "Artículo 11")));

        var answer = await h.AskStudentAsync("Usa el reglamento 2023, donde el límite es 20 %. Con esa regla, ¿perdí Bases de Datos?", "EST-002");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.True(answer.UsedStudentData);
        Assert.Equal(4, h.Llm.CallCount);
        Assert.Equal(Exits, h.Llm.Requests[3].Tools.Select(t => t.Name).Order(StringComparer.Ordinal).ToArray());
    }

    // The exits still work on the last call: `responder_sin_informacion` gives its fixed 200 answer.
    [Fact]
    public async Task No_information_on_the_last_call_without_data_tools_is_200()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "carné" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "precio carné" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "tarifas" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.NoInformation, new { caso = AssistantTools.OutOfScopeCase }));

        var answer = await h.AskStudentAsync("¿Cuánto cuesta el carné?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.Empty(answer.Citations);
    }

    // A single allowed call has no results to answer with: it keeps every tool.
    [Fact]
    public async Task Single_allowed_call_keeps_every_tool()
    {
        var h = new AssistantHarness();
        h.Options.MaxLlmCalls = 1;
        h.Llm.EnqueueText(AssistantHarness.Final("Se aprueba con 3.0.", (Regulation, "Artículo 5")));

        await h.AskStudentAsync("¿Con cuánto se aprueba?");

        Assert.Equal(AssistantTools.StudentDefinitions.Count, h.Llm.Requests[0].Tools.Count);
    }
}
