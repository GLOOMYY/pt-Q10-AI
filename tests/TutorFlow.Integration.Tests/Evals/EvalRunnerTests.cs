using System.Text.Json;
using TutorFlow.Evals.Adapters;
using TutorFlow.Evals.Ports;
using TutorFlow.Evals.Reporting;
using TutorFlow.Evals.Runner;
using TutorFlow.Integration.Tests.Scripts;
using TutorFlow.Integration.Tests.Support;
using TutorFlow.Scenarios;
using TutorFlow.Testing.AcademicApi;
using TutorFlow.Testing.Audit;
using Xunit;

namespace TutorFlow.Integration.Tests.Evals;

/// <summary>
/// The <c>evals/</c> runner (R-QA-01) checked WITHOUT the real LLM: it runs against the backend in process, with the audit
/// log and the simulated API as doubles of its two ports. Only the scenarios the deterministic layers resolve before the
/// model are run, because with the fake LLM nothing that depends on the text could pass (that is the job of F5).
/// </summary>
public sealed class EvalRunnerTests
{
    private sealed class InMemoryAuditSource(InMemoryAuditLog log) : IAuditSource
    {
        public Task<IReadOnlyList<JsonElement>> FindAsync(string correlationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<JsonElement>>([.. log.FindAll(correlationId).Select(r => JsonSerializer.SerializeToElement(r))]);
    }

    private sealed class FakeApiSwitch(FakeAcademicApi api) : IMockApiSwitch
    {
        public List<string> Calls { get; } = [];

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Calls.Add("stop");
            api.Stop();
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Calls.Add("start");
            api.Start();
            return Task.CompletedTask;
        }
    }

    private static EvalRunner Runner(IntegrationHost host, FakeApiSwitch apiSwitch, HttpClient http) =>
        new(
            http,
            new InMemoryAuditSource(host.Audit),
            apiSwitch,
            new ScenarioChecker(GlobalCitationRules.Load()),
            new EvalRunOptions("test", TimeSpan.Zero, TimeSpan.FromMilliseconds(200)));

    [Fact]
    public async Task Scenarios_resolved_before_the_model_pass_with_every_check_of_the_evals_column()
    {
        var scenarios = ScenarioData.ApiScenarios
            .Where(s => ScenarioScripts.For(s.Id)!.Kind == ScriptKind.NoModel)
            .ToList();
        Assert.Equal(40, scenarios.Count);
        await using var host = new IntegrationHost();
        var apiSwitch = new FakeApiSwitch(host.Api);
        using var http = host.CreateClient();

        var results = await Runner(host, apiSwitch, http).RunAsync(scenarios, CancellationToken.None);

        Assert.Equal(40, results.Count);
        Assert.All(results, r => Assert.True(r.Status == EvalStatus.Passed, $"{r.Scenario.Id}: {string.Join(" | ", r.Failures)}"));
        Assert.All(results, r => Assert.StartsWith("evals-test-ESC-", r.CorrelationId, StringComparison.Ordinal));
        Assert.Equal(40, results.Select(r => r.CorrelationId).Distinct().Count());
    }

    [Fact]
    public async Task The_simulated_api_is_stopped_for_the_detenida_scenarios_and_always_started_again()
    {
        var detenida = ScenarioData.ApiScenarios.Where(s => s.Precondition == Precondition.ApiDown).ToList();
        Assert.NotEmpty(detenida);
        await using var host = new IntegrationHost();
        var apiSwitch = new FakeApiSwitch(host.Api);
        using var http = host.CreateClient();

        var results = await Runner(host, apiSwitch, http).RunAsync(detenida, CancellationToken.None);

        Assert.Equal(["stop", "start"], apiSwitch.Calls);
        Assert.All(results, r => Assert.Equal(EvalStatus.Passed, r.Status));
        Assert.All(results, r => Assert.Equal(503, r.HttpStatus));
        Assert.False(host.Api.IsStopped);
    }

    [Fact]
    public async Task The_quota_exhausted_scenarios_are_never_run_by_evals()
    {
        var quota = ScenarioData.ApiScenarios.Where(s => s.Precondition == Precondition.LlmQuotaExhausted).ToList();
        Assert.NotEmpty(quota);
        await using var host = new IntegrationHost();
        var apiSwitch = new FakeApiSwitch(host.Api);
        using var http = host.CreateClient();

        var results = await Runner(host, apiSwitch, http).RunAsync(quota, CancellationToken.None);

        Assert.All(results, r => Assert.Equal(EvalStatus.NotRun, r.Status));
        Assert.Empty(host.Audit.Records);
        Assert.Empty(apiSwitch.Calls);
    }

    [Fact]
    public async Task A_wrong_expectation_fails_with_the_check_that_failed()
    {
        var scenario = ScenarioData.Get("ESC-ENT-01") with
        {
            Expected = JsonElement.Parse("""{"status":200,"respuesta":{"contiene":["3.0"]}}"""),
        };
        await using var host = new IntegrationHost();
        using var http = host.CreateClient();

        var results = await Runner(host, new FakeApiSwitch(host.Api), http).RunAsync([scenario], CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(EvalStatus.Failed, result.Status);
        Assert.Contains(result.Failures, f => f.StartsWith("status: expected 200 but got 400", StringComparison.Ordinal));
        Assert.Contains(result.Failures, f => f.StartsWith("respuesta.contiene", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_unknown_generation_marker_makes_the_scenario_fail()
    {
        var body = JsonElement.Parse("""{"metodo":"POST","ruta":"/api/asistente/preguntar","cuerpo":{"pregunta":"<generar: algo desconocido>","usuario":{"rol":"estudiante","id":"EST-001"}}}""");
        var scenario = ScenarioData.Get("ESC-ENT-03") with { Input = body };
        await using var host = new IntegrationHost();
        using var http = host.CreateClient();

        var result = Assert.Single(await Runner(host, new FakeApiSwitch(host.Api), http).RunAsync([scenario], CancellationToken.None));

        Assert.Equal(EvalStatus.Failed, result.Status);
        Assert.Contains("Unknown generation marker", result.Failures[0], StringComparison.Ordinal);
        Assert.Empty(host.Audit.Records);
    }

    private sealed class BrokenSwitch(bool failStop, bool failStart) : IMockApiSwitch
    {
        public Task StopAsync(CancellationToken cancellationToken) =>
            failStop ? Task.FromException(new System.ComponentModel.Win32Exception("docker not found")) : Task.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken) =>
            failStart ? Task.FromException(new InvalidOperationException("restart failed")) : Task.CompletedTask;
    }

    [Theory]
    [InlineData(true, false, "could not be stopped")]
    [InlineData(false, true, "could not be started again")]
    public async Task Docker_problems_mark_the_detenida_scenarios_as_failed_with_the_reason(bool failStop, bool failStart, string expected)
    {
        var detenida = ScenarioData.ApiScenarios.Where(s => s.Precondition == Precondition.ApiDown).ToList();
        await using var host = new IntegrationHost();
        using var http = host.CreateClient();
        var runner = new EvalRunner(
            http,
            new InMemoryAuditSource(host.Audit),
            new BrokenSwitch(failStop, failStart),
            new ScenarioChecker(GlobalCitationRules.Load()),
            new EvalRunOptions("test", TimeSpan.Zero, TimeSpan.FromMilliseconds(200)));

        var results = await runner.RunAsync(detenida, CancellationToken.None);

        Assert.All(results, r => Assert.Equal(EvalStatus.Failed, r.Status));
        Assert.All(results, r => Assert.Contains(r.Failures, f => f.Contains(expected, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task The_probe_question_detects_a_backend_without_an_operational_llm()
    {
        var probe = ScenarioData.Get("ESC-BAN-01");

        await using var down = new IntegrationHost();
        down.Llm.SimulateQuotaExhausted();
        using var downHttp = down.CreateClient();
        var broken = await Runner(down, new FakeApiSwitch(down.Api), downHttp).ProbeAsync(probe, CancellationToken.None);
        Assert.Contains("no tiene un LLM real operativo", broken, StringComparison.Ordinal);
        Assert.Contains("503", broken, StringComparison.Ordinal);

        await using var zero = new IntegrationHost();
        zero.Llm.EnqueueText("""{"respuesta":"3.0","citas":[]}""");
        zero.Llm.Respond(_ => TutorFlow.Testing.Llm.FakeLlmReply.FromText("""{"respuesta":"3.0","citas":[]}""").WithTokens(0, 0));
        using var zeroHttp = zero.CreateClient();
        Assert.Contains("tokens = 0", await Runner(zero, new FakeApiSwitch(zero.Api), zeroHttp).ProbeAsync(probe, CancellationToken.None), StringComparison.Ordinal);

        await using var ok = new IntegrationHost();
        ok.Llm.Respond(_ => TutorFlow.Testing.Llm.FakeLlmReply.FromText("""{"respuesta":"3.0","citas":[]}"""));
        using var okHttp = ok.CreateClient();
        Assert.Null(await Runner(ok, new FakeApiSwitch(ok.Api), okHttp).ProbeAsync(probe, CancellationToken.None));
    }

    [Fact]
    public async Task The_jsonl_audit_source_finds_the_line_of_a_correlation_id_even_with_a_partial_last_line()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tutorflow-audit-{Guid.NewGuid():N}.jsonl");
        try
        {
            await File.WriteAllLinesAsync(path,
            [
                """{"correlationId":"evals-1-ESC-A-01","status":200}""",
                """{"correlationId":"evals-1-ESC-A-02","status":403}""",
                """{"correlationId":"evals-1-ESC-A-0""",
            ]);
            var source = new JsonlAuditSource(path);

            var found = await source.FindAsync("evals-1-ESC-A-02", CancellationToken.None);

            Assert.Equal(403, Assert.Single(found).GetProperty("status").GetInt32());
            Assert.Empty(await source.FindAsync("evals-1-ESC-A-09", CancellationToken.None));
            Assert.Empty(await new JsonlAuditSource(path + ".missing").FindAsync("x", CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void The_report_groups_by_file_and_lists_every_failure_with_its_requirements()
    {
        var banco = ScenarioData.Get("ESC-BAN-01");
        var entradas = ScenarioData.Get("ESC-ENT-01");
        var results = new List<EvalResult>
        {
            new(banco, EvalStatus.Passed, [], 4, 200, 10, "c1", "{}"),
            new(entradas, EvalStatus.Failed, ["status: expected 400 but got 200."], 3, 200, 10, "c2", "{\"respuesta\":\"x\"}"),
        };
        var directory = Path.Combine(Path.GetTempPath(), $"tutorflow-report-{Guid.NewGuid():N}");
        try
        {
            var text = ReportWriter.ToText(results);
            Assert.Contains("| banco.md | 1 | 1 | 0 | 0 |", text, StringComparison.Ordinal);
            Assert.Contains("| entradas.md | 1 | 0 | 1 | 0 |", text, StringComparison.Ordinal);
            Assert.Contains("ESC-ENT-01", text, StringComparison.Ordinal);
            Assert.Contains("status: expected 400 but got 200.", text, StringComparison.Ordinal);
            Assert.Contains(entradas.Verifies[0], text, StringComparison.Ordinal);

            var (markdown, json) = ReportWriter.Write(directory, "r1", results);
            Assert.True(File.Exists(markdown));
            using var document = JsonDocument.Parse(File.ReadAllText(json));
            Assert.Equal(1, document.RootElement.GetProperty("totals").GetProperty("Failed").GetInt32());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
