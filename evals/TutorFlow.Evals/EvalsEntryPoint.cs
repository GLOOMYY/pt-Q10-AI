using System.Globalization;
using TutorFlow.Evals.Adapters;
using TutorFlow.Evals.Reporting;
using TutorFlow.Evals.Runner;
using TutorFlow.Scenarios;

namespace TutorFlow.Evals;

/// <summary>
/// Entry point of <c>evals/</c> (R-QA-01): runs the <c>api</c> scenarios of <c>escenarios/</c> against the system started
/// with <c>docker compose</c>, with the real LLM. Only in F5; never in CI.
/// </summary>
public static class EvalsEntryPoint
{
    private const string KeyVariable = "LLM_API_KEY";

    /// <summary>Runs the evals.</summary>
    /// <param name="args">Command line arguments (see <see cref="EvalOptions.Usage"/>).</param>
    /// <returns>0 if every scenario passed, 1 if any failed, 2 if the run could not start.</returns>
    public static async Task<int> Main(string[] args)
    {
        EvalOptions options;
        try
        {
            options = EvalOptions.Parse(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or UriFormatException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(EvalOptions.Usage);
            return 2;
        }

        if (options.Help)
        {
            Console.WriteLine(EvalOptions.Usage);
            return 0;
        }

        var root = FindRoot();
        var load = ScenarioCatalog.Load(root);
        foreach (var error in load.Errors)
        {
            Console.Error.WriteLine($"FORMAT ERROR {error.Id} ({error.File}): {error.Message}");
        }

        var selected = Select(load.Scenarios, options);
        if (options.ListOnly)
        {
            return List(load, selected);
        }

        var prerequisite = CheckPrerequisites(options, root);
        if (prerequisite is not null)
        {
            Console.Error.WriteLine(prerequisite);
            return 2;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        var runId = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4]}";
        using var http = new HttpClient { BaseAddress = options.BaseUrl, Timeout = options.RequestTimeout };
        var unhealthy = await CheckHealthAsync(http, cancellation.Token).ConfigureAwait(false);
        if (unhealthy is not null)
        {
            Console.Error.WriteLine(unhealthy);
            return 2;
        }

        var auditPath = Path.GetFullPath(options.AuditFile, root);
        var runner = new EvalRunner(
            http,
            new JsonlAuditSource(auditPath),
            new DockerComposeMockApiSwitch(root),
            new ScenarioChecker(GlobalCitationRules.Load(root)),
            new EvalRunOptions(runId, options.Delay, TimeSpan.FromSeconds(10)),
            message => Console.WriteLine(message));

        var results = (await runner.RunAsync(selected, cancellation.Token).ConfigureAwait(false)).ToList();

        // A scenario that does not follow the grammar is a failure of the run, not something silently left out (README section 6).
        foreach (var error in load.Errors.Where(e => options.Only.Count == 0 || options.Only.Contains(e.Id)))
        {
            results.Add(new EvalResult(
                new Scenario(error.Id, "format error", error.File, [], ScenarioLevel.Api, Precondition.ApiUp, default, default),
                EvalStatus.Failed,
                [$"Format error: {error.Message}"],
                0,
                null,
                0,
                null,
                null));
        }

        Console.WriteLine();
        Console.WriteLine(ReportWriter.ToText(results));
        var (markdown, json) = ReportWriter.Write(Path.GetFullPath(options.ReportDirectory, root), runId, results);
        Console.WriteLine($"Report: {markdown}");
        Console.WriteLine($"Report: {json}");
        return results.Any(r => r.Status == EvalStatus.Failed) ? 1 : 0;
    }

    private static async Task<string?> CheckHealthAsync(HttpClient http, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(new Uri("/api/salud", UriKind.Relative), cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? null
                : $"GET /api/salud answered {(int)response.StatusCode}. Start the system first: docker compose up -d --build --wait";
        }
        catch (HttpRequestException ex)
        {
            return $"The system is not reachable at {http.BaseAddress}: {ex.Message}. Start it first: docker compose up -d --build --wait";
        }
    }

    private static string FindRoot()
    {
        try
        {
            return RepositoryLocator.FindRoot(Directory.GetCurrentDirectory());
        }
        catch (DirectoryNotFoundException)
        {
            return RepositoryLocator.FindRoot();
        }
    }

    private static List<Scenario> Select(IReadOnlyList<Scenario> scenarios, EvalOptions options) =>
        [.. scenarios.Where(s =>
            (options.Only.Count == 0 || options.Only.Contains(s.Id, StringComparer.Ordinal))
            && (options.Files.Count == 0 || options.Files.Contains(s.File, StringComparer.Ordinal)))];

    private static int List(ScenarioLoadResult load, List<Scenario> selected)
    {
        foreach (var scenario in selected)
        {
            var action = scenario.Level == ScenarioLevel.Unit ? "tests/ only (unit)"
                : scenario.Precondition == Precondition.LlmQuotaExhausted ? "tests/ only (llm-cuota-agotada)"
                : "evals/ and tests/";
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{scenario.Id}\t{scenario.File}\t{scenario.Level}\t{scenario.Precondition}\t{action}\t{scenario.Title}"));
        }

        var api = selected.Count(s => s.Level == ScenarioLevel.Api);
        var evals = selected.Count(s => s.Level == ScenarioLevel.Api && s.Precondition != Precondition.LlmQuotaExhausted);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{selected.Count} scenarios, {api} api, {evals} run by evals/; {load.Errors.Count} format errors."));
        return load.Errors.Count == 0 ? 0 : 1;
    }

    // The real key must be in the environment of this process (never read from .env, never printed); a fake key means the run
    // would not use the real LLM, so it is refused. The backend gets the same key from its own configuration (.env of compose).
    private static string? CheckPrerequisites(EvalOptions options, string root)
    {
        var key = Environment.GetEnvironmentVariable(KeyVariable);
        if (string.IsNullOrWhiteSpace(key))
        {
            return $"{KeyVariable} is not set in the environment of this process. The evals use the real LLM: export the real key (it is never read from .env).";
        }

        if (key.Contains("ficticia", StringComparison.OrdinalIgnoreCase) || key.StartsWith("ci-", StringComparison.OrdinalIgnoreCase))
        {
            return $"{KeyVariable} looks like a fake key (example or CI value). The evals need the real one.";
        }

        var audit = Path.GetFullPath(options.AuditFile, root);
        if (!Directory.Exists(Path.GetDirectoryName(audit)))
        {
            return $"The audit directory does not exist: {Path.GetDirectoryName(audit)}. Start the system first: docker compose up -d --build --wait";
        }

        return null;
    }
}
