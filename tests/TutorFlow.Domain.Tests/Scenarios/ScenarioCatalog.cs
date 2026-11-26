using System.Text.Json;
using System.Text.RegularExpressions;

namespace TutorFlow.Domain.Tests.Scenarios;

/// <summary>A scenario of level <c>unitario</c> read from <c>escenarios/</c> (escenarios/README.md §6).</summary>
/// <param name="Id">Scenario id, e.g. <c>ESC-RAR-01</c>.</param>
/// <param name="Function">Canonical function name (README §4.2).</param>
/// <param name="Arguments">The <c>argumentos</c> object of the input.</param>
/// <param name="Expected">The <c>resultado</c> value of the expected output.</param>
internal sealed record UnitScenario(string Id, string Function, JsonElement Arguments, JsonElement Expected);

/// <summary>
/// Reads the scenario files following the parsing grammar of escenarios/README.md §6, so the tests never copy or
/// redefine a scenario: they cannot diverge from the files. A malformed scenario throws (it is a format failure).
/// </summary>
internal static partial class ScenarioCatalog
{
    [GeneratedRegex(@"^### (ESC-[A-Z]+-\d{2}) · (.+)$")]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"^- \*\*Nivel:\*\* (api|unitario)$")]
    private static partial Regex LevelRegex();

    [GeneratedRegex(@"^- \*\*Verifica:\*\* (.+)$")]
    private static partial Regex VerifiesRegex();

    [GeneratedRegex(@"^- \*\*Precondiciones:\*\* (ninguna|api-simulada-levantada|api-simulada-detenida|llm-cuota-agotada)$")]
    private static partial Regex PreconditionsRegex();

    /// <summary>Absolute path of the repository root (the folder that contains <c>escenarios/README.md</c>).</summary>
    public static string RepositoryRoot { get; } = FindRoot();

    /// <summary>
    /// Loads every scenario of a file and returns the ones of level <c>unitario</c>.
    /// </summary>
    /// <param name="fileName">File name inside <c>escenarios/</c>, e.g. <c>casos_raros.md</c>.</param>
    /// <param name="idPrefix">Prefix every id of the file must have, e.g. <c>ESC-RAR</c>.</param>
    public static IReadOnlyList<UnitScenario> LoadUnit(string fileName, string idPrefix)
    {
        string path = Path.Combine(RepositoryRoot, "escenarios", fileName);
        string[] lines = File.ReadAllLines(path);
        var result = new List<UnitScenario>();

        int i = 0;
        while (i < lines.Length)
        {
            Match header = HeaderRegex().Match(lines[i]);
            if (!header.Success)
            {
                i++;
                continue;
            }

            string id = header.Groups[1].Value;
            if (!id.StartsWith(idPrefix + "-", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{id} does not use the prefix {idPrefix} of {fileName}.");
            }

            int start = i + 1;
            int end = start;
            while (end < lines.Length && !IsScenarioEnd(lines[end]))
            {
                end++;
            }

            string[] body = lines[start..end];
            i = end;

            string? level = Single(body, LevelRegex(), id, "Nivel");
            Single(body, VerifiesRegex(), id, "Verifica");
            Single(body, PreconditionsRegex(), id, "Precondiciones");
            using JsonDocument input = JsonDocument.Parse(JsonBlockAfter(body, "**Input**", id));
            using JsonDocument output = JsonDocument.Parse(JsonBlockAfter(body, "**Output esperado**", id));

            if (level != "unitario")
            {
                continue;
            }

            string function = input.RootElement.GetProperty("funcion").GetString()
                ?? throw new InvalidDataException($"{id} has no function.");
            result.Add(new UnitScenario(
                id,
                function,
                input.RootElement.GetProperty("argumentos").Clone(),
                output.RootElement.GetProperty("resultado").Clone()));
        }

        return result;
    }

    private static bool IsScenarioEnd(string line) =>
        line == "---" || line.StartsWith("## ", StringComparison.Ordinal) || line.StartsWith("### ", StringComparison.Ordinal);

    private static string? Single(string[] body, Regex regex, string id, string field)
    {
        Match[] matches = [.. body.Select(l => regex.Match(l)).Where(m => m.Success)];
        if (matches.Length != 1)
        {
            throw new InvalidDataException($"{id}: field {field} missing, duplicated or with an invalid value.");
        }

        return matches[0].Groups[1].Value;
    }

    private static string JsonBlockAfter(string[] body, string marker, string id)
    {
        int at = Array.IndexOf(body, marker);
        if (at < 0)
        {
            throw new InvalidDataException($"{id}: missing {marker}.");
        }

        int open = Array.FindIndex(body, at + 1, l => l == "```json");
        int close = open < 0 ? -1 : Array.FindIndex(body, open + 1, l => l == "```");
        if (close < 0)
        {
            throw new InvalidDataException($"{id}: missing json block after {marker}.");
        }

        return string.Join('\n', body[(open + 1)..close]);
    }

    private static string FindRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "escenarios", "README.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("escenarios/README.md not found above the test binaries.");
    }
}
