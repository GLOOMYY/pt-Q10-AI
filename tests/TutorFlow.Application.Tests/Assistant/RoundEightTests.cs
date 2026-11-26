using TutorFlow.Application.Assistant;
using TutorFlow.Application.Assistant.Answering;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Tests.Support;
using TutorFlow.Testing.Llm;
using Xunit;

namespace TutorFlow.Application.Tests.Assistant;

/// <summary>
/// Eighth round of F5 corrections (ESC-COO-08, DUDA-29): the last allowed call is sent with no tool at all, not even
/// <c>rechazar</c>. R-ARQ-04 and D-32 ask the model to decide the rejection before using data tools, and
/// <c>rechazar</c> is offered on every earlier call; the deterministic authorization of the data tools is unchanged.
/// </summary>
public class RoundEightTests
{
    private const string Regulation = "reglamento_academico_2025.md";

    // ESC-COO-08: a coordinator asks about a student of their program; after three rounds of authorized tools the last
    // call declares no tool and the model answers with the data: 200.
    [Fact]
    public async Task Last_call_declares_no_tool_and_the_coordinator_gets_the_answer()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.FindStudent, new { nombre = "Laura Gómez" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus, new { estudianteId = "EST-001" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "habilitación" }))
            .EnqueueText(AssistantHarness.Final(
                "Laura Gómez (EST-001) no tiene asignaturas reprobadas que pueda habilitar.",
                ("Datos del programa", "ING-SIS · periodo 2026-2"),
                (Regulation, "Artículo 6")));

        var answer = await h.AskCoordinatorAsync("¿Laura Gómez puede habilitar?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.True(answer.UsedStudentData);
        Assert.Equal(4, h.Llm.CallCount);
        Assert.Empty(h.Llm.Requests[3].Tools);
        Assert.EndsWith(SystemInstructions.LastCall, h.Llm.Requests[3].SystemInstruction, StringComparison.Ordinal);
        Assert.All(h.Llm.Requests.Take(3), r => Assert.Contains(r.Tools, t => t.Name == AssistantTools.Reject));
    }

    // A `rechazar` asked for on the last call was not offered: it is not run nor logged, and the request is a 503.
    [Fact]
    public async Task Reject_on_the_last_call_is_not_run_and_is_503()
    {
        var h = new AssistantHarness();
        h.Llm.EnforceRejectBeforeDataTools = false; // On purpose: the model asks for it on the last call.
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.FindStudent, new { nombre = "Laura Gómez" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus, new { estudianteId = "EST-001" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "habilitación" }))
            .EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.Reject, new { motivo = AuditReasons.ThirdPartyData }));

        var answer = await h.AskCoordinatorAsync("¿Laura Gómez puede habilitar?");

        Assert.Equal(AssistantOutcome.Unavailable, answer.Outcome);
        Assert.Equal(4, h.Llm.CallCount);
        Assert.Empty(h.Llm.Requests[3].Tools);
        Assert.DoesNotContain(AssistantTools.Reject, h.Log.Tools);
        Assert.Equal(
            [AssistantTools.FindStudent, AssistantTools.AcademicStatus, AssistantTools.SearchRegulations],
            h.Log.Tools);
    }

    // R-ARQ-04: `rechazar` on any of the first three calls is still the 403, with its reason.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Reject_on_the_first_three_calls_is_403(int previousRounds)
    {
        var h = new AssistantHarness();
        h.Llm.EnforceRejectBeforeDataTools = false; // On purpose: the decision may come after other tools.
        for (var i = 0; i < previousRounds; i++)
        {
            h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "notas" }));
        }

        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.Reject, new { motivo = AuditReasons.ThirdPartyData }));

        var answer = await h.AskStudentAsync("¿Qué nota sacó EST-003?");

        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.Equal(AuditReasons.ThirdPartyData, answer.Reason);
        Assert.Contains(h.Llm.Requests[previousRounds].Tools, t => t.Name == AssistantTools.Reject);
        Assert.Equal(AssistantTools.Reject, h.Log.Tools[^1]);
    }

    // A single allowed call keeps every tool, `rechazar` included: its 403 still works.
    [Fact]
    public async Task Single_allowed_call_keeps_reject()
    {
        var h = new AssistantHarness();
        h.Options.MaxLlmCalls = 1;
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.Reject, new { motivo = AuditReasons.ThirdPartyData }));

        var answer = await h.AskStudentAsync("¿Qué nota sacó EST-003?");

        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.Contains(h.Llm.Requests[0].Tools, t => t.Name == AssistantTools.Reject);
    }
}
