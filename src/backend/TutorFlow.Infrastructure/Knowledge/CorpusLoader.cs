using System.Text;

namespace TutorFlow.Infrastructure.Knowledge;

/// <summary>Locates and reads the corpus files, then parses them into citable sections.</summary>
public static class CorpusLoader
{
    private const string RelativeCorpusDirectory = "material/reglamento";

    /// <summary>Loads every indexed document (R-FUE-01) and fails clearly if a file is missing.</summary>
    /// <param name="configuredPath">Configured directory, or empty to probe the default locations.</param>
    /// <returns>All the citable sections.</returns>
    /// <exception cref="DirectoryNotFoundException">The corpus directory could not be found.</exception>
    /// <exception cref="FileNotFoundException">An indexed document is missing from the directory.</exception>
    public static IReadOnlyList<CorpusSection> Load(string? configuredPath)
    {
        var directory = ResolveDirectory(configuredPath);
        var sections = new List<CorpusSection>();
        foreach (var document in CorpusDocuments.Indexed)
        {
            var path = Path.Combine(directory, document.FileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Corpus document '{document.FileName}' not found in '{directory}' ({KnowledgeOptions.CorpusPathVariable}).",
                    path);
            }

            sections.AddRange(CorpusParser.Parse(document, File.ReadAllText(path, Encoding.UTF8)));
        }

        return sections;
    }

    private static string ResolveDirectory(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!Directory.Exists(configuredPath))
            {
                throw new DirectoryNotFoundException(
                    $"Corpus directory '{configuredPath}' does not exist ({KnowledgeOptions.CorpusPathVariable}).");
            }

            return configuredPath;
        }

        // Probe next to the binaries first (Docker image), then walk up (repository checkout, tests).
        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, RelativeCorpusDirectory);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Corpus directory '{RelativeCorpusDirectory}' not found from '{AppContext.BaseDirectory}'. " +
            $"Set {KnowledgeOptions.CorpusPathVariable}.");
    }
}
