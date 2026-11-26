using System.Text.Json;

namespace TutorFlow.Scenarios;

/// <summary>A citation as it appears in a response: document and section, compared exactly.</summary>
/// <param name="Document">The <c>documento</c>.</param>
/// <param name="Section">The <c>seccion</c>.</param>
public sealed record ObservedCitation(string Document, string Section)
{
    /// <inheritdoc />
    public override string ToString() => $"{Document} · {Section}";
}

/// <summary>
/// The global list of allowed citations (escenarios/README section 4.3), read from the README itself and from
/// <c>material/mock-api/datos.json</c> (period and program ids), never copied.
/// </summary>
public sealed class GlobalCitationRules
{
    private readonly HashSet<string> _prohibitedDocuments;
    private readonly Dictionary<string, HashSet<string>> _allowed;

    private GlobalCitationRules(HashSet<string> prohibited, Dictionary<string, HashSet<string>> allowed)
    {
        _prohibitedDocuments = prohibited;
        _allowed = allowed;
    }

    /// <summary>Reads the rules of the repository.</summary>
    /// <param name="repositoryRoot">Repository root; located from the binaries when <see langword="null"/>.</param>
    /// <returns>The rules.</returns>
    public static GlobalCitationRules Load(string? repositoryRoot = null)
    {
        var root = repositoryRoot ?? RepositoryLocator.FindRoot();
        var readme = File.ReadAllLines(Path.Combine(root, "escenarios", "README.md"));
        var heading = Array.FindIndex(readme, l => l.StartsWith("### 4.3 ", StringComparison.Ordinal));
        var open = heading < 0 ? -1 : Array.FindIndex(readme, heading, l => l.TrimEnd() == "```json");
        var close = open < 0 ? -1 : Array.FindIndex(readme, open + 1, l => l.TrimEnd() == "```");
        if (close < 0)
        {
            throw new InvalidOperationException("The JSON block of README section 4.3 was not found.");
        }

        using var rules = JsonDocument.Parse(string.Join('\n', readme[(open + 1)..close]));
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "material", "mock-api", "datos.json")));
        var period = data.RootElement.GetProperty("periodo").GetString()!;
        var programIds = data.RootElement.GetProperty("programas").EnumerateArray().Select(p => p.GetProperty("id").GetString()!).ToList();

        var prohibited = rules.RootElement.GetProperty("documentosProhibidos").EnumerateArray()
            .Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
        var allowed = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var document in rules.RootElement.GetProperty("documentos").EnumerateObject())
        {
            allowed[document.Name] = document.Value.EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
        }

        foreach (var document in rules.RootElement.GetProperty("datos").EnumerateObject())
        {
            var sections = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pattern in document.Value.EnumerateArray().Select(e => e.GetString()!))
            {
                var withPeriod = pattern.Replace("<periodo>", period, StringComparison.Ordinal);
                if (withPeriod.Contains("<programaId>", StringComparison.Ordinal))
                {
                    foreach (var id in programIds)
                    {
                        sections.Add(withPeriod.Replace("<programaId>", id, StringComparison.Ordinal));
                    }
                }
                else
                {
                    sections.Add(withPeriod);
                }
            }

            allowed[document.Name] = sections;
        }

        return new GlobalCitationRules(prohibited, allowed);
    }

    /// <summary>Whether a citation is allowed (exact comparison).</summary>
    /// <param name="citation">The citation.</param>
    /// <returns><see langword="true"/> if it is valid.</returns>
    public bool IsAllowed(ObservedCitation citation)
    {
        ArgumentNullException.ThrowIfNull(citation);
        return !_prohibitedDocuments.Contains(citation.Document)
            && _allowed.TryGetValue(citation.Document, out var sections)
            && sections.Contains(citation.Section);
    }
}
