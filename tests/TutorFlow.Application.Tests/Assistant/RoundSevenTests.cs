using TutorFlow.Application.Assistant;
using TutorFlow.Application.Assistant.Answering;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Tests.Support;
using TutorFlow.Testing.Llm;
using Xunit;

namespace TutorFlow.Application.Tests.Assistant;

/// <summary>
/// Seventh round of F5 corrections: `responder_sin_informacion` is only offered on the first call of a question,
/// before any tool result (R-RES-03, R-RES-07). From the second call on the model writes the answer with the results it
/// has (ESC-COO-08, ESC-COO-09, ESC-INY-13, ESC-INY-14, ESC-INY-16); `rechazar` stays on every call (R-ARQ-04).
/// </summary>
public class RoundSevenTests
{
    private const string Regulation = "reglamento_academico_2025.md";

    private static string[] Names(IEnumerable<TutorFlow.Application.Ports.Llm.LlmToolDefinition> tools) =>
        [.. tools.Select(t => t.Name)];

    // The first call offers the exit; with no tools used, it is still the fixed 200 answer.
    [Fact]
    public async Task First_call_offers_no_information_and_keeps_its_fixed_answer()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.NoInformation, new { caso = AssistantTools.OutOfScopeCase }));

        var answer = await h.AskStudentAsync("¿Quién ganó la final de la Champions?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.Equal(CitationValidator.NoInformationText, answer.Text);
        Assert.Empty(answer.Citations);
        Assert.False(answer.UsedStudentData);
        Assert.Contains(AssistantTools.NoInformation, Names(h.Llm.Requests[0].Tools));
        Assert.DoesNotContain(SystemInstructions.LaterCall, h.Llm.Requests[0].SystemInstruction, StringComparison.Ordinal);
    }

    // ESC-INY-16: the second call does not declare the exit, keeps every other tool and tells the model to write.
    [Fact]
    public async Task Second_call_does_not_declare_no_information()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "aprobación" }))
            .EnqueueText(AssistantHarness.Final("Se aprueba con 3.0.", (Regulation, "Artículo 5")));

        var answer = await h.AskStudentAsync("¿Con cuánto se aprueba una materia?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        var second = Names(h.Llm.Requests[1].Tools);
        Assert.DoesNotContain(AssistantTools.NoInformation, second);
        Assert.Contains(AssistantTools.Reject, second);
        Assert.Contains(AssistantTools.SearchRegulations, second);
        Assert.Equal(AssistantTools.StudentDefinitions.Count - 1, second.Length);
        Assert.EndsWith(SystemInstructions.LaterCall, h.Llm.Requests[1].SystemInstruction, StringComparison.Ordinal);
    }

    // ESC-COO-08, ESC-COO-09: three rounds of tools; the fourth call only offers `rechazar` and its text is a 200 that
    // uses student data.
    [Fact]
    public async Task Fourth_call_offers_only_reject_and_answers_with_the_data()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.FindStudent, new { nombre = "Laura Gómez" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus, new { estudianteId = "EST-001" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "beca por rendimiento" }))
            .EnqueueText(AssistantHarness.Final(
                "Laura Gómez (EST-001) cumple la beca por rendimiento.",
                ("Datos del programa", "ING-SIS · periodo 2026-2")));

        var answer = await h.AskCoordinatorAsync("¿Laura Gómez cumple la beca por rendimiento?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.True(answer.UsedStudentData);
        Assert.Equal(4, h.Llm.CallCount);
        Assert.Equal([AssistantTools.Reject], Names(h.Llm.Requests[3].Tools));
        Assert.All(h.Llm.Requests.Skip(1), r => Assert.DoesNotContain(AssistantTools.NoInformation, Names(r.Tools)));
    }

    // ESC-INY-13: a non-offered exit together with a data tool in a later call: the data tool runs, the exit does not,
    // and the model is asked once to write the answer with those results.
    [Fact]
    public async Task Non_offered_no_information_with_a_data_tool_runs_only_the_data_tool()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "promedio" }))
            .EnqueueToolCalls(
                FakeLlmReply.Call(AssistantTools.AcademicStatus),
                FakeLlmReply.Call(AssistantTools.NoInformation, new { caso = AssistantTools.OutOfScopeCase }))
            .EnqueueText(AssistantHarness.Final(
                "Tu promedio ponderado del periodo es 4.39.",
                ("Datos del estudiante", "Notas · periodo 2026-2")));

        var answer = await h.AskStudentAsync("¿Cuál es mi promedio?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.True(answer.UsedStudentData);
        Assert.Equal([AssistantTools.SearchRegulations, AssistantTools.AcademicStatus], h.Log.Tools);
        Assert.Equal(["EST-001"], h.Log.StudentsConsulted);
    }

    // R-ARQ-04: `rechazar` is still a 403 on any call (second, third and last).
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Reject_works_on_any_call(int rejectCall)
    {
        var h = new AssistantHarness();
        h.Llm.EnforceRejectBeforeDataTools = false; // On purpose: the decision comes after other tools.
        for (var i = 0; i < rejectCall; i++)
        {
            h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "notas" }));
        }

        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.Reject, new { motivo = AuditReasons.ThirdPartyData }));

        var answer = await h.AskStudentAsync("¿Qué nota sacó EST-003?");

        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.Equal(AuditReasons.ThirdPartyData, answer.Reason);
        Assert.Equal(rejectCall + 1, h.Llm.CallCount);
        Assert.All(h.Llm.Requests, r => Assert.Contains(AssistantTools.Reject, Names(r.Tools)));
    }
}
