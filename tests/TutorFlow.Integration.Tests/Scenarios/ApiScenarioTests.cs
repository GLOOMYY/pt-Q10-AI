using TutorFlow.Integration.Tests.Scripts;
using TutorFlow.Integration.Tests.Support;
using TutorFlow.Scenarios;
using Xunit;
using Xunit.Abstractions;

namespace TutorFlow.Integration.Tests.Scenarios;

/// <summary>
/// R-QA-01, AC-01..04: every <c>api</c> scenario of <c>escenarios/</c> end to end against the real backend in process, with
/// the fake LLM, the double of the simulated API and the in-memory audit log. Only what the <c>tests/</c> column of
/// escenarios/README section 4.4 says is checked; the rest (text of the answer, citations of a 200, tokens "&gt;0") is
/// verified by <c>evals/</c> with the real LLM.
/// </summary>
public sealed class ApiScenarioTests(ITestOutputHelper output)
{
    /// <summary>One test case per scenario id.</summary>
    /// <param name="id">Scenario id.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(ScenarioData.Runnable), MemberType = typeof(ScenarioData))]
    public async Task Scenario_passes_end_to_end(string id)
    {
        var scenario = ScenarioData.Get(id);
        var script = ScenarioScripts.For(id);
        Assert.True(script is not null, $"{id}: the scenario has no script for the fake LLM (Scripts/ScenarioScripts.cs). Define one; do not skip it.");

        var result = await ScenarioExecutor.RunAsync(scenario, script);
        if (result.Outcome.ChecksRun == 0 && result.Outcome.Failures.Count == 0)
        {
            // Not a false "passed": the scenario has nothing that tests/ may verify (README 4.4) and says so.
            Assert.Fail($"{id}: sin comprobaciones en tests/ (all its fields are evaluated only by evals/). Declare it explicitly instead of passing.");
        }

        output.WriteLine($"{id}: status {result.Observed.Status}, {result.Outcome.ChecksRun} checks, LLM calls {result.LlmCalls}.");
        Assert.True(
            result.Outcome.Passed,
            $"{id} ({string.Join(", ", scenario.Verifies)}) failed:{Environment.NewLine}  - {string.Join(Environment.NewLine + "  - ", result.Outcome.Failures)}");
    }

    /// <summary>Scenarios listed as a known defect of the backend: each must still fail (visible, never skipped).</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Known_defects_still_fail()
    {
        foreach (var id in KnownDefects.Ids)
        {
            var script = ScenarioScripts.For(id);
            Assert.NotNull(script);
            var result = await ScenarioExecutor.RunAsync(ScenarioData.Get(id), script);
            output.WriteLine($"{id}: known defect. {KnownDefects.Describe(id)}");
            Assert.False(result.Outcome.Passed, $"{id} passes now: the defect is fixed, remove it from KnownDefects.");
        }
    }
}
