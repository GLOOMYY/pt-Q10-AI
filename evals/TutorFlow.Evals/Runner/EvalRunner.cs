using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TutorFlow.Evals.Ports;
using TutorFlow.Scenarios;

namespace TutorFlow.Evals.Runner;

/// <summary>Outcome of one scenario in <c>evals/</c>.</summary>
public enum EvalStatus
{
    /// <summary>All the checks of the <c>evals/</c> column passed.</summary>
    Passed,

    /// <summary>At least one check failed (or the scenario could not run).</summary>
    Failed,

    /// <summary>Not executed here: <c>llm-cuota-agotada</c> only runs in <c>tests/</c> (README section 2.1).</summary>
    NotRun,
}

/// <summary>Result of one scenario.</summary>
/// <param name="Scenario">The scenario.</param>
/// <param name="Status">Passed, failed or not run.</param>
/// <param name="Failures">One message per failed check (or the reason it was not run).</param>
/// <param name="ChecksRun">Number of checks applied.</param>
/// <param name="HttpStatus">HTTP status of the main request, if it was made.</param>
/// <param name="ElapsedMs">Duration of the main request.</param>
/// <param name="CorrelationId">The <c>X-Correlation-Id</c> sent.</param>
/// <param name="Body">Response body (kept for the report of failures).</param>
public sealed record EvalResult(
    Scenario Scenario,
    EvalStatus Status,
    IReadOnlyList<string> Failures,
    int ChecksRun,
    int? HttpStatus,
    long ElapsedMs,
    string? CorrelationId,
    string? Body);

/// <summary>Settings of a run.</summary>
/// <param name="RunId">Short unique id of the run, part of every correlation id.</param>
/// <param name="DelayBetweenRequests">Pause between scenarios (to respect the LLM rate limits).</param>
/// <param name="AuditWait">How long to wait for the audit record of a request to show up in the file.</param>
public sealed record EvalRunOptions(string RunId, TimeSpan DelayBetweenRequests, TimeSpan AuditWait);

/// <summary>
/// Runs the <c>api</c> scenarios against the system under test with the real LLM (README section 4.4, <c>evals/</c>
/// column): sends the request of each scenario with its own unique <c>X-Correlation-Id</c>, finds its audit record by
/// that id (README section 5.2) and applies every check. <c>api-simulada-detenida</c> scenarios run together while the
/// simulated API is stopped, which is always started again afterwards; <c>llm-cuota-agotada</c> is never run.
/// </summary>
public sealed class EvalRunner(
    HttpClient http,
    IAuditSource audit,
    IMockApiSwitch mockApi,
    ScenarioChecker checker,
    EvalRunOptions options,
    Action<string>? progress = null)
{
    private const string CorrelationHeader = "X-Correlation-Id";

    /// <summary>Runs the scenarios.</summary>
    /// <param name="scenarios">The scenarios to run (those of level <c>unitario</c> are ignored).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>One result per <c>api</c> scenario, in the order they were given.</returns>
    public async Task<IReadOnlyList<EvalResult>> RunAsync(IReadOnlyList<Scenario> scenarios, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        var api = scenarios.Where(s => s.Level == ScenarioLevel.Api).ToList();
        var results = new Dictionary<string, EvalResult>(StringComparer.Ordinal);

        foreach (var scenario in api.Where(s => s.Precondition == Precondition.ApiUp))
        {
            results[scenario.Id] = await RunOneAsync(scenario, cancellationToken).ConfigureAwait(false);
            await Task.Delay(options.DelayBetweenRequests, cancellationToken).ConfigureAwait(false);
        }

        var stopped = api.Where(s => s.Precondition == Precondition.ApiDown).ToList();
        if (stopped.Count > 0)
        {
            foreach (var (id, result) in await RunWithApiStoppedAsync(stopped, cancellationToken).ConfigureAwait(false))
            {
                results[id] = result;
            }
        }

        foreach (var scenario in api.Where(s => s.Precondition == Precondition.LlmQuotaExhausted))
        {
            results[scenario.Id] = new EvalResult(
                scenario, EvalStatus.NotRun, ["llm-cuota-agotada only runs in tests/ (README section 2.1): the real LLM is never exhausted on purpose."], 0, null, 0, null, null);
        }

        return [.. api.Select(s => results[s.Id])];
    }

    private async Task<List<(string Id, EvalResult Result)>> RunWithApiStoppedAsync(List<Scenario> stopped, CancellationToken cancellationToken)
    {
        var results = new List<(string, EvalResult)>();
        progress?.Invoke("Stopping the simulated API (mock-api)...");
        try
        {
            await mockApi.StopAsync(cancellationToken).ConfigureAwait(false);
            foreach (var scenario in stopped)
            {
                results.Add((scenario.Id, await RunOneAsync(scenario, cancellationToken).ConfigureAwait(false)));
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            // The API could not be stopped: the scenarios that were not run fail with the reason, they are not skipped.
            foreach (var scenario in stopped.Where(s => results.TrueForAll(r => r.Item1 != s.Id)))
            {
                results.Add((scenario.Id, Failed(scenario, $"The simulated API could not be stopped: {ex.Message}")));
            }
        }
        finally
        {
            progress?.Invoke("Starting the simulated API again...");

            // Never leave the stack without the simulated API, even if the run is cancelled: no token here on purpose.
            await mockApi.StartAsync(CancellationToken.None).ConfigureAwait(false);
        }

        return results;
    }

    private static EvalResult Failed(Scenario scenario, string reason) =>
        new(scenario, EvalStatus.Failed, [reason], 0, null, 0, null, null);

    private async Task<EvalResult> RunOneAsync(Scenario scenario, CancellationToken cancellationToken)
    {
        progress?.Invoke($"{scenario.Id} {scenario.Title}");
        ScenarioRequest request;
        try
        {
            request = ScenarioRequest.From(scenario);
        }
        catch (InvalidOperationException ex)
        {
            return Failed(scenario, ex.Message); // Unknown <generar: …> marker: the scenario fails (README section 3.3).
        }

        var correlationId = $"evals-{options.RunId}-{scenario.Id}";
        var clock = Stopwatch.StartNew();
        try
        {
            using var response = await SendAsync(request, correlationId, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            clock.Stop();

            var failures = new List<string>();
            if (!response.Headers.TryGetValues(CorrelationHeader, out var echoed) || echoed.SingleOrDefault() != correlationId)
            {
                failures.Add("R-LOG-09: the response does not echo the X-Correlation-Id sent.");
            }

            var after = await SendAfterAsync(scenario, correlationId, cancellationToken).ConfigureAwait(false);
            var record = await FindRecordAsync(correlationId, failures, cancellationToken).ConfigureAwait(false);

            var observed = new ObservedResult((int)response.StatusCode, body, record, after);
            var outcome = checker.Check(scenario, observed, CheckSite.Evals);
            failures.AddRange(outcome.Failures);
            return new EvalResult(
                scenario,
                failures.Count == 0 ? EvalStatus.Passed : EvalStatus.Failed,
                failures,
                outcome.ChecksRun,
                (int)response.StatusCode,
                clock.ElapsedMilliseconds,
                correlationId,
                body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            clock.Stop();
            return new EvalResult(
                scenario, EvalStatus.Failed, [$"Transport error calling the system under test: {ex.Message}"], 0, null, clock.ElapsedMilliseconds, correlationId, null);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(ScenarioRequest request, string correlationId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(new HttpMethod(request.Method), new Uri(request.Route, UriKind.Relative));
        message.Headers.Add(CorrelationHeader, correlationId);
        if (request.Body is not null)
        {
            message.Content = new StringContent(request.Body, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        }

        return await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<int>> SendAfterAsync(Scenario scenario, string correlationId, CancellationToken cancellationToken)
    {
        var statuses = new List<int>();
        if (!scenario.Expected.TryGetProperty("despues", out var after))
        {
            return statuses;
        }

        var index = 0;
        foreach (var next in after.EnumerateArray())
        {
            var request = new ScenarioRequest(next.GetProperty("metodo").GetString()!, next.GetProperty("ruta").GetString()!, null);
            using var response = await SendAsync(request, $"{correlationId}-after{index++}", cancellationToken).ConfigureAwait(false);
            statuses.Add((int)response.StatusCode);
        }

        return statuses;
    }

    // README 5.2: exactly one line has this correlationId. The backend writes it when the response is decided, so poll briefly.
    private async Task<JsonElement?> FindRecordAsync(string correlationId, List<string> failures, CancellationToken cancellationToken)
    {
        var deadline = Stopwatch.StartNew();
        IReadOnlyList<JsonElement> records;
        do
        {
            records = await audit.FindAsync(correlationId, cancellationToken).ConfigureAwait(false);
            if (records.Count > 0)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }
        while (deadline.Elapsed < options.AuditWait);

        if (records.Count == 1)
        {
            return records[0];
        }

        failures.Add($"R-LOG-01, R-LOG-09: expected exactly one audit record with correlationId '{correlationId}' but found {records.Count}.");
        return null;
    }
}
