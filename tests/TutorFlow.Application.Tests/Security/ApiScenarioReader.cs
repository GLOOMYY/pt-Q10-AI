using System.Text.Json;
using System.Text.RegularExpressions;

namespace TutorFlow.Application.Tests.Security;

/// <summary>An <c>api</c> scenario of <c>escenarios/</c> reduced to what the security tests need.</summary>
/// <param name="Id">Scenario id, e.g. <c>ESC-INY-01</c>.</param>
/// <param name="Question">The <c>pregunta</c> sent.</param>
/// <param name="Role">The <c>usuario.rol</c> sent.</param>
/// <param name="UserId">The <c>usuario.id</c> sent.</param>
/// <param name="Status">Expected HTTP status, or <see langword="null"/> if not evaluated.</param>
/// <param name="Reason">Expected <c>log.motivo</c>, or <see langword="null"/> if not evaluated.</param>
public sealed record ApiScenario(string Id, string Question, string Role, string UserId, int? Status, string? Reason);

/// <summary>
/// Reads the scenarios straight from <c>escenarios/*.md</c> following the grammar of README §6, without copying or
/// redefining them. Scenarios whose body is not a JSON object with a string question (invalid bodies, generated values)
/// are skipped.
/// </summary>
public static partial class ApiScenarioReader
{
    /// <summary>Reads every readable scenario of the repository.</summary>
    /// <returns>The scenarios.</returns>
    public static IReadOnlyList<ApiScenario> ReadAll()
    {
        var directory = FindScenariosDirectory();
        var result = new List<ApiScenario>();
        foreach (var file in Directory.GetFiles(directory, "*.md").Order(StringComparer.Ordinal))
        {
            if (Path.GetFileName(file).Equals("README.md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.AddRange(ReadFile(File.ReadAllText(file)));
        }

        return result;
    }

    private static IEnumerable<ApiScenario> ReadFile(string text)
    {
        var headers = ScenarioHeader().Matches(text);
        for (var i = 0; i < headers.Count; i++)
        {
            var end = i + 1 < headers.Count ? headers[i + 1].Index : text.Length;
            var block = text[headers[i].Index..end];
            var scenario = TryParse(headers[i].Groups[1].Value, block);
            if (scenario is not null)
            {
                yield return scenario;
            }
        }
    }

    private static ApiScenario? TryParse(string id, string block)
    {
        var input = JsonAfter(block, "**Input**");
        var output = JsonAfter(block, "**Output esperado**");
        if (input is null || output is null)
        {
            return null;
        }

        using (input)
        using (output)
        {
            if (!input.RootElement.TryGetProperty("cuerpo", out var body) || body.ValueKind != JsonValueKind.Object
                || !body.TryGetProperty("pregunta", out var question) || question.ValueKind != JsonValueKind.String
                || !body.TryGetProperty("usuario", out var user) || user.ValueKind != JsonValueKind.Object
                || !user.TryGetProperty("rol", out var role) || role.ValueKind != JsonValueKind.String
                || !user.TryGetProperty("id", out var userId) || userId.ValueKind != JsonValueKind.String
                || question.GetString()!.StartsWith("<generar", StringComparison.Ordinal))
            {
                return null;
            }

            int? status = output.RootElement.TryGetProperty("status", out var s) ? s.GetInt32() : null;
            string? reason = output.RootElement.TryGetProperty("log", out var log)
                && log.TryGetProperty("motivo", out var m) && m.ValueKind == JsonValueKind.String
                    ? m.GetString()
                    : null;
            return new ApiScenario(id, question.GetString()!, role.GetString()!, userId.GetString()!, status, reason);
        }
    }

    private static JsonDocument? JsonAfter(string block, string marker)
    {
        var at = block.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var match = JsonFence().Match(block, at);
        return match.Success ? JsonDocument.Parse(match.Groups[1].Value) : null;
    }

    private static string FindScenariosDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "escenarios");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The escenarios/ directory was not found above the test binaries.");
    }

    [GeneratedRegex(@"^### (ESC-[A-Z]+-\d{2}) · .+$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ScenarioHeader();

    [GeneratedRegex(@"```json\r?\n(.*?)\r?\n```", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex JsonFence();
}
