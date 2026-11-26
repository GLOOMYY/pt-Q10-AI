using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using TutorFlow.Evals.Runner;

namespace TutorFlow.Evals.Reporting;

/// <summary>Counts of a group of results.</summary>
/// <param name="Passed">Scenarios that passed.</param>
/// <param name="Failed">Scenarios that failed.</param>
/// <param name="NotRun">Scenarios that are not run in <c>evals/</c>.</param>
public sealed record Totals(int Passed, int Failed, int NotRun)
{
    /// <summary>All scenarios of the group.</summary>
    public int Total => Passed + Failed + NotRun;
}

/// <summary>
/// Builds the report of a run: a console summary and two files (JSON and Markdown) with passed/failed per scenario, grouped
/// by file of scenarios, with each failure and the SPEC requirements it affects (F5 hands this to the orchestrator).
/// </summary>
public static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Counts the results of a group.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The totals.</returns>
    public static Totals Count(IEnumerable<EvalResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var list = results.ToList();
        return new Totals(
            list.Count(r => r.Status == EvalStatus.Passed),
            list.Count(r => r.Status == EvalStatus.Failed),
            list.Count(r => r.Status == EvalStatus.NotRun));
    }

    /// <summary>Plain text for the console (and the Markdown file).</summary>
    /// <param name="results">The results.</param>
    /// <returns>The report text.</returns>
    public static string ToText(IReadOnlyList<EvalResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var text = new StringBuilder();
        text.AppendLine("# Evals report");
        text.AppendLine();
        text.AppendLine("| File | Scenarios | Passed | Failed | Not run |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var group in results.GroupBy(r => r.Scenario.File))
        {
            var totals = Count(group);
            text.AppendLine(CultureInfo.InvariantCulture, $"| {group.Key} | {totals.Total} | {totals.Passed} | {totals.Failed} | {totals.NotRun} |");
        }

        var all = Count(results);
        text.AppendLine(CultureInfo.InvariantCulture, $"| **Total** | {all.Total} | {all.Passed} | {all.Failed} | {all.NotRun} |");

        var failed = results.Where(r => r.Status == EvalStatus.Failed).ToList();
        text.AppendLine();
        text.AppendLine(failed.Count == 0 ? "## Failures: none" : "## Failures");
        foreach (var result in failed)
        {
            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"### {result.Scenario.Id} · {result.Scenario.Title}");
            text.AppendLine(CultureInfo.InvariantCulture, $"- Verifies: {string.Join(", ", result.Scenario.Verifies)}");
            text.AppendLine(CultureInfo.InvariantCulture, $"- HTTP status: {(result.HttpStatus?.ToString(CultureInfo.InvariantCulture) ?? "none")} · correlationId: {result.CorrelationId ?? "none"}");
            foreach (var failure in result.Failures)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- FAIL: {failure}");
            }

            if (result.Body is { Length: > 0 } body)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- Response: {(body.Length > 600 ? body[..600] + "…" : body)}");
            }
        }

        var notRun = results.Where(r => r.Status == EvalStatus.NotRun).ToList();
        if (notRun.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("## Not run in evals/");
            foreach (var result in notRun)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- {result.Scenario.Id}: {string.Join(' ', result.Failures)}");
            }
        }

        return text.ToString();
    }

    /// <summary>Writes the Markdown and JSON reports.</summary>
    /// <param name="directory">Directory (created if needed); it must be ignored by git, e.g. <c>var/evals</c>.</param>
    /// <param name="runId">Id of the run, used in the file names.</param>
    /// <param name="results">The results.</param>
    /// <returns>The paths written (Markdown, JSON).</returns>
    public static (string Markdown, string Json) Write(string directory, string runId, IReadOnlyList<EvalResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        Directory.CreateDirectory(directory);
        var markdown = Path.Combine(directory, $"informe-{runId}.md");
        var json = Path.Combine(directory, $"informe-{runId}.json");
        File.WriteAllText(markdown, ToText(results), new UTF8Encoding(false));

        var document = new
        {
            runId,
            totals = Count(results),
            files = results.GroupBy(r => r.Scenario.File).Select(g => new { file = g.Key, totals = Count(g) }),
            scenarios = results.Select(r => new
            {
                id = r.Scenario.Id,
                file = r.Scenario.File,
                title = r.Scenario.Title,
                verifica = r.Scenario.Verifies,
                status = r.Status.ToString(),
                httpStatus = r.HttpStatus,
                checks = r.ChecksRun,
                elapsedMs = r.ElapsedMs,
                correlationId = r.CorrelationId,
                failures = r.Failures,
                response = r.Status == EvalStatus.Failed ? r.Body : null,
            }),
        };
        File.WriteAllText(json, JsonSerializer.Serialize(document, JsonOptions), new UTF8Encoding(false));
        return (markdown, json);
    }
}
