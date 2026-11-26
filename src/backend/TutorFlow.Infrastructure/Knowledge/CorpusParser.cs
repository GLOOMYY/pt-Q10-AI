using System.Text;
using System.Text.RegularExpressions;

namespace TutorFlow.Infrastructure.Knowledge;

/// <summary>
/// Splits a corpus document into citable sections by reading its structure, so the citation index always mirrors the
/// real files (R-FUE-02, CL-18) and no list of articles lives in the code.
/// </summary>
public static partial class CorpusParser
{
    [GeneratedRegex(@"^\*\*Art[ií]culo\s+(\d+)\.", RegexOptions.CultureInvariant)]
    private static partial Regex ArticleStart();

    [GeneratedRegex(@"^(#{1,6})\s+(?:(\d+(?:\.\d+)*)\.?\s+)?(.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex Heading();

    /// <summary>Parses one document.</summary>
    /// <param name="document">Document descriptor.</param>
    /// <param name="content">Full markdown content.</param>
    /// <returns>The citable sections, in document order.</returns>
    public static IReadOnlyList<CorpusSection> Parse(CorpusDocument document, string content)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(content);
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return document.Kind == CorpusDocumentKind.Articles
            ? ParseArticles(document, lines)
            : ParseNumberedSections(document, lines);
    }

    private static List<CorpusSection> ParseArticles(CorpusDocument document, string[] lines)
    {
        var result = new List<CorpusSection>();
        string? label = null;
        var body = new StringBuilder();

        void Flush()
        {
            if (label is not null)
            {
                result.Add(new CorpusSection(document.FileName, label, body.ToString().Trim()));
            }

            label = null;
            body.Clear();
        }

        foreach (var line in lines)
        {
            var article = ArticleStart().Match(line);
            if (article.Success)
            {
                Flush();
                label = $"Artículo {article.Groups[1].Value}";
            }
            else if (line.StartsWith('#'))
            {
                // A chapter heading closes the open article.
                Flush();
            }

            if (label is not null)
            {
                body.AppendLine(line);
            }
        }

        Flush();
        return result;
    }

    private static List<CorpusSection> ParseNumberedSections(CorpusDocument document, string[] lines)
    {
        // First pass: group lines under their heading. Headings without a number (the title, the annex) are not
        // citable and close the previous section, so the annex content is never indexed (D-07).
        var blocks = new List<(string? Number, StringBuilder Body)>();
        (string? Number, StringBuilder Body)? current = null;

        foreach (var line in lines)
        {
            var heading = Heading().Match(line);
            if (heading.Success && heading.Groups[1].Length >= 2)
            {
                if (current is not null)
                {
                    blocks.Add(current.Value);
                }

                var number = heading.Groups[2].Success ? heading.Groups[2].Value : null;
                current = (number, new StringBuilder());
            }
            else if (heading.Success)
            {
                // Level 1 heading: the document title.
                if (current is not null)
                {
                    blocks.Add(current.Value);
                    current = null;
                }

                continue;
            }

            current?.Body.AppendLine(line);
        }

        if (current is not null)
        {
            blocks.Add(current.Value);
        }

        var numbers = blocks.Where(b => b.Number is not null).Select(b => b.Number!).ToList();
        var result = new List<CorpusSection>();
        foreach (var (number, body) in blocks)
        {
            // A heading that has sub-sections (e.g. "3. Becas" with 3.1 and 3.2) is a container, not citable (R-FUE-02).
            if (number is null || numbers.Any(n => n.StartsWith(number + ".", StringComparison.Ordinal)))
            {
                continue;
            }

            result.Add(new CorpusSection(document.FileName, $"Sección {number}", body.ToString().Trim()));
        }

        return result;
    }
}
