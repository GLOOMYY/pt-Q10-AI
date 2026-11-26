using Xunit;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Ports.Llm;
using TutorFlow.Testing.Llm;

namespace TutorFlow.Infrastructure.Tests.Llm;

/// <summary>The fake LLM is shared by every test project: its contract is verified here (D-32, CL-23, R-RES-05).</summary>
public sealed class FakeLlmClientTests
{
    private static readonly LlmRequest Request = new("sys", [LlmMessage.User("hola")], []);

    [Fact]
    public async Task ReturnsScriptedRepliesInOrderAndRecordsRequests()
    {
        var llm = new FakeLlmClient().EnqueueToolCalls(FakeLlmReply.Call("consultar", new { a = 1 })).EnqueueText("listo");

        var first = await llm.CompleteAsync(Request, CancellationToken.None);
        var second = await llm.CompleteAsync(Request, CancellationToken.None);

        Assert.Equal("consultar", Assert.Single(first.ToolCalls).Name);
        Assert.Equal("listo", second.Text);
        Assert.Equal(2, llm.CallCount);
    }

    [Fact]
    public void NoCalls_MeansCallCountZero() => Assert.Equal(0, new FakeLlmClient().CallCount);

    [Fact]
    public async Task UnscriptedCall_FailsLoudly() =>
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new FakeLlmClient().CompleteAsync(Request, CancellationToken.None));

    [Fact]
    public async Task QuotaExhausted_ThrowsDependencyUnavailableForTheLlm()
    {
        var llm = new FakeLlmClient().SimulateQuotaExhausted();

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => llm.CompleteAsync(Request, CancellationToken.None));

        Assert.Equal(ExternalDependency.Llm, ex.Dependency);
        Assert.Equal(1, llm.CallCount);
    }

    [Fact]
    public void Reject_MustComeBeforeDataTools()
    {
        Assert.Throws<ArgumentException>(() => FakeLlmReply.FromToolCalls(
            FakeLlmReply.Call("consultar_notas"), FakeLlmReply.Call("rechazar", new { motivo = "x" })));

        var ok = FakeLlmReply.FromToolCalls(
            FakeLlmReply.Call("rechazar", new { motivo = "x" }), FakeLlmReply.Call("consultar_notas"));
        Assert.Equal("rechazar", ok.ToolCalls[0].Name);
    }

    [Fact]
    public async Task Usage_UsesConfigurablePricesWithoutRounding()
    {
        var llm = new FakeLlmClient().Enqueue(FakeLlmReply.FromText("x").WithTokens(1000, 100, 50));

        var response = await llm.CompleteAsync(Request, CancellationToken.None);

        Assert.Equal(1150, response.Usage.TotalTokens);
        Assert.Equal(0.000475m, response.Usage.CostUsd);
    }
}
