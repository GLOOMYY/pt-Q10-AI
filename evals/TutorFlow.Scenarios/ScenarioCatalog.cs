using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TutorFlow.Scenarios;

/// <summary>
/// Reads the scenarios from <c>escenarios/</c> with the grammar of README section 6, without interpretation. Only the
/// files of the index (README section 8) are processed; <c>README.md</c> never is (its <c>ESC-EJ-01</c> is illustrative).
/// </summary>
public static partial class ScenarioCatalog
{
    /// <summary>Reads every scenario of the repository.</summary>
    /// <param name="repositoryRoot">Root of the repository; located from the binaries when <see langword="null"/>.</param>
    /// <returns>The scenarios and the format errors.</returns>
    public static ScenarioLoadResult Load(string? repositoryRoot = null)
    {
        var root = repositoryRoot ?? RepositoryLocator.FindRoot();
        var directory = Path.Combine(root, "escenarios");
        var index = ReadIndex(File.ReadAllText(Path.Combine(directory, "README.md")));
        var scenarios = new List<Scenario>();
        var errors = new List<ScenarioFormatError>();
        foreach (var (file, (prefix, _)) in index)
        {
            var path = Path.Combine(directory, file);
            if (!File.Exists(path))
            {
                errors.Add(new ScenarioFormatError(file, file, "File listed in the index does not exist."));
                continue;
            }

            ParseFile(file, prefix, File.ReadAllLines(path), scenarios, errors);
        }

        return new ScenarioLoadResult(scenarios, errors, index);
    }

    /// <summary>Reads the index table of README section 8 (file, prefix, declared count).</summary>
    /// <param name="readme">Text of <c>escenarios/README.md</c>.</param>
    /// <returns>File name to prefix and declared count, in index order.</returns>
    public static IReadOnlyDictionary<string, (string Prefix, int Count)> ReadIndex(string readme)
    {
        ArgumentNullException.ThrowIfNull(readme);
        var result = new Dictionary<string, (string Prefix, int Count)>(StringComparer.Ordinal);
        var start = readme.IndexOf("## 8. Índice de tipos", StringComparison.Ordinal);
        if (start < 0)
        {
            return result;
        }

        foreach (Match match in IndexRow().Matches(readme, start))
        {
            result[match.Groups[2].Value] = (match.Groups[1].Value, int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
        }

        return result;
    }

    private static void ParseFile(
        string file, string prefix, string[] lines, List<Scenario> scenarios, List<ScenarioFormatError> errors)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var header = Header().Match(lines[i].TrimEnd('\r'));
            if (!header.Success)
            {
                continue;
            }

            // Rule 3: the scenario ends at the first following line that is `---`, `## ` or `### `, or at the end of the file.
            var end = i + 1;
            while (end < lines.Length && !IsBoundary(lines[end]))
            {
                end++;
            }

            var id = header.Groups[1].Value;
            var block = lines[(i + 1)..end].Select(l => l.TrimEnd('\r')).ToArray();
            try
            {
                if (!id.StartsWith(prefix + "-", StringComparison.Ordinal))
                {
                    throw new FormatException($"The id prefix must be '{prefix}' for {file}.");
                }

                scenarios.Add(ParseBlock(id, header.Groups[2].Value, file, block));
            }
            catch (Exception ex) when (ex is FormatException or JsonException)
            {
                errors.Add(new ScenarioFormatError(id, file, ex.Message));
            }

            i = end - 1;
        }
    }

    private static bool IsBoundary(string line)
    {
        var text = line.TrimEnd('\r');
        return text == "---" || text.StartsWith("## ", StringComparison.Ordinal) || text.StartsWith("### ", StringComparison.Ordinal);
    }

    private static Scenario ParseBlock(string id, string title, string file, string[] block)
    {
        var verifies = Single(block, VerifiesLine(), "Verifica").Split(", ", StringSplitOptions.RemoveEmptyEntries);
        var level = Single(block, LevelLine(), "Nivel") == "api" ? ScenarioLevel.Api : ScenarioLevel.Unit;
        var precondition = Single(block, PreconditionLine(), "Precondiciones") switch
        {
            "ninguna" => Precondition.None,
            "api-simulada-levantada" => Precondition.ApiUp,
            "api-simulada-detenida" => Precondition.ApiDown,
            _ => Precondition.LlmQuotaExhausted,
        };
        var input = JsonBlockAfter(block, "**Input**");
        var expected = JsonBlockAfter(block, "**Output esperado**");
        return new Scenario(id, title, file, verifies, level, precondition, input, expected);
    }

    private static string Single(string[] block, Regex pattern, string name)
    {
        var matches = block.Select(l => pattern.Match(l)).Where(m => m.Success).ToList();
        return matches.Count == 1
            ? matches[0].Groups[1].Value
            : throw new FormatException($"Expected exactly one valid '{name}' line, found {matches.Count}.");
    }

    private static JsonElement JsonBlockAfter(string[] block, string marker)
    {
        var at = Array.FindIndex(block, l => l == marker);
        if (at < 0)
        {
            throw new FormatException($"The '{marker}' line is missing.");
        }

        var open = Array.FindIndex(block, at + 1, l => l == "```json");
        if (open < 0)
        {
            throw new FormatException($"No ```json block after '{marker}'.");
        }

        var close = Array.FindIndex(block, open + 1, l => l == "```");
        if (close < 0)
        {
            throw new FormatException($"The ```json block after '{marker}' is not closed.");
        }

        using var document = JsonDocument.Parse(string.Join('\n', block[(open + 1)..close]));
        return document.RootElement.Clone();
    }

    [GeneratedRegex(@"^### (ESC-[A-Z]+-\d{2}) · (.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex Header();

    [GeneratedRegex(@"^- \*\*Verifica:\*\* (.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex VerifiesLine();

    [GeneratedRegex(@"^- \*\*Nivel:\*\* (api|unitario)$", RegexOptions.CultureInvariant)]
    private static partial Regex LevelLine();

    [GeneratedRegex(@"^- \*\*Precondiciones:\*\* (ninguna|api-simulada-levantada|api-simulada-detenida|llm-cuota-agotada)$", RegexOptions.CultureInvariant)]
    private static partial Regex PreconditionLine();

    [GeneratedRegex(@"^\|[^|]*\|\s*`(ESC-[A-Z]+)`\s*\|\s*`([a-z_]+\.md)`\s*\|\s*(\d+)\s*\|", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex IndexRow();
}
