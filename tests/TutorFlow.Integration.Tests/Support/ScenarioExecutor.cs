using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TutorFlow.Integration.Tests.Scripts;
using TutorFlow.Scenarios;

namespace TutorFlow.Integration.Tests.Support;

/// <summary>Result of running one scenario in process.</summary>
/// <param name="Outcome">Failures and number of checks that applied in <c>tests/</c>.</param>
/// <param name="Observed">What was observed.</param>
/// <param name="LlmCalls">How many times the fake LLM was called.</param>
public sealed record ExecutionResult(CheckOutcome Outcome, ObservedResult Observed, int LlmCalls);

/// <summary>
/// Runs an <c>api</c> scenario end to end against the real backend in process: its precondition (escenarios/README
/// section 2.1), the request exactly as the scenario says (with the <c>&lt;generar&gt;</c> markers resolved), the
/// <c>despues</c> requests and the checks that README section 4.4 assigns to <c>tests/</c>.
/// </summary>
public static class ScenarioExecutor
{
    private static readonly GlobalCitationRules GlobalRules = GlobalCitationRules.Load();
    private static readonly ScenarioChecker Checker = new(GlobalRules);

    /// <summary>Executes a scenario with its script.</summary>
    /// <param name="scenario">An <c>api</c> scenario.</param>
    /// <param name="script">What the fake model does.</param>
    /// <returns>The result, with the failures (including the ones about the script itself).</returns>
    public static async Task<ExecutionResult> RunAsync(Scenario scenario, ModelScript script)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(script);
        await using var host = new IntegrationHost();
        Prepare(host, scenario, script);

        var correlationId = $"it-{scenario.Id}-{Guid.NewGuid():N}";
        var request = ScenarioRequest.From(scenario);
        using var client = host.CreateClient();
        using var response = await SendAsync(client, request, correlationId);
        var body = await response.Content.ReadAsStringAsync();

        var problems = new List<string>();
        if (!response.Headers.TryGetValues(IntegrationHost.CorrelationHeader, out var echoed) || echoed.Single() != correlationId)
        {
            problems.Add("R-LOG-09: the response does not echo the X-Correlation-Id of the request.");
        }

        var after = await SendAfterAsync(client, scenario);
        var records = host.Audit.FindAll(correlationId);
        JsonElement? record = null;
        if (records.Count == 1)
        {
            record = JsonSerializer.SerializeToElement(records[0]);
        }
        else if (scenario.Expected.TryGetProperty("log", out _) || records.Count > 1)
        {
            problems.Add($"R-LOG-01: expected exactly one audit record for the request but found {records.Count}.");
        }

        var observed = new ObservedResult((int)response.StatusCode, body, record, after);
        var outcome = Checker.Check(scenario, observed, CheckSite.Tests);
        var llmCalls = host.Llm.CallCount;
        if (script.Kind == ScriptKind.NoModel && llmCalls > 0)
        {
            problems.Add($"Script: {scenario.Id} is declared as resolved before the model, but the backend called the LLM {llmCalls} time(s).");
        }

        return new ExecutionResult(
            new CheckOutcome([.. problems, .. outcome.Failures], outcome.ChecksRun),
            observed,
            llmCalls);
    }

    private static void Prepare(IntegrationHost host, Scenario scenario, ModelScript script)
    {
        switch (scenario.Precondition)
        {
            case Precondition.ApiDown:
                host.Api.Stop();
                break;
            case Precondition.LlmQuotaExhausted:
                host.Llm.SimulateQuotaExhausted();
                break;
        }

        foreach (var turn in script.TurnList)
        {
            host.Llm.Enqueue(turn);
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, ScenarioRequest request, string correlationId)
    {
        using var message = new HttpRequestMessage(new HttpMethod(request.Method), new Uri(request.Route, UriKind.Relative));
        message.Headers.Add(IntegrationHost.CorrelationHeader, correlationId);
        if (request.Body is not null)
        {
            message.Content = new StringContent(request.Body, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        }

        return await client.SendAsync(message);
    }

    private static async Task<IReadOnlyList<int>> SendAfterAsync(HttpClient client, Scenario scenario)
    {
        var statuses = new List<int>();
        if (!scenario.Expected.TryGetProperty("despues", out var after))
        {
            return statuses;
        }

        foreach (var next in after.EnumerateArray())
        {
            var request = new ScenarioRequest(next.GetProperty("metodo").GetString()!, next.GetProperty("ruta").GetString()!, null);
            using var response = await SendAsync(client, request, $"it-after-{Guid.NewGuid():N}");
            statuses.Add((int)response.StatusCode);
        }

        return statuses;
    }
}
