using System.Globalization;
using System.Text;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Ports.Knowledge;

namespace TutorFlow.Infrastructure.Knowledge;

/// <summary>
/// In-memory, deterministic knowledge base over the three indexed documents (R-FUE-01). Search is keyword based
/// (accent-insensitive); no embeddings are needed for a corpus this small, and results are reproducible (R-QA-03).
/// </summary>
public sealed class KnowledgeBase : IKnowledgeBase
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "los", "las", "una", "uno", "unos", "unas", "del", "que", "por", "con", "para", "como", "cual", "cuales",
        "cuando", "donde", "quien", "mis", "sus", "este", "esta", "esto", "ese", "esa", "son", "ser", "hay", "puedo",
        "pero", "mas", "muy", "sin", "sobre", "entre", "segun", "mi", "tu", "su", "el", "la", "lo", "de", "en", "y",
        "o", "a", "un", "al", "se", "es", "si", "no",
    };

    private readonly IReadOnlyList<IndexedSection> _sections;
    private readonly HashSet<(string Document, string Section)> _citable;

    /// <summary>Builds the index from already parsed sections.</summary>
    /// <param name="sections">The citable sections of the corpus.</param>
    public KnowledgeBase(IReadOnlyList<CorpusSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        _sections = sections.Select(s => new IndexedSection(s, Tokenize(s.Text))).ToList();
        _citable = sections.Select(s => (s.Document, s.Section)).ToHashSet();
    }

    /// <summary>Creates the knowledge base reading the corpus files.</summary>
    /// <param name="corpusPath">Corpus directory, or empty to probe the default locations.</param>
    /// <returns>The knowledge base.</returns>
    public static KnowledgeBase FromDirectory(string? corpusPath) => new(CorpusLoader.Load(corpusPath));

    /// <summary>Every citable section, for diagnostics and tests.</summary>
    public IEnumerable<Citation> AllCitations =>
        _sections.Select(s => new Citation(s.Section.Document, s.Section.Section));

    /// <inheritdoc />
    public Task<IReadOnlyList<KnowledgeFragment>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
        {
            return Task.FromResult<IReadOnlyList<KnowledgeFragment>>([]);
        }

        var queryTerms = Tokenize(query);
        var requestedArticles = RequestedArticleNumbers(query);

        IReadOnlyList<KnowledgeFragment> ranked = _sections
            .Select((s, index) => (Section: s, Index: index, Score: Score(s, queryTerms, requestedArticles)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Index)
            .Take(maxResults)
            .Select(x => new KnowledgeFragment(
                new Citation(x.Section.Section.Document, x.Section.Section.Section),
                x.Section.Section.Text))
            .ToList();

        return Task.FromResult(ranked);
    }

    /// <inheritdoc />
    public bool IsCitable(Citation citation)
    {
        // R-FUE-02, CL-18: exact, ordinal match against the real index; anything else is discarded.
        return citation is not null
            && citation.Document is not null
            && citation.Section is not null
            && _citable.Contains((citation.Document, citation.Section));
    }

    private static int Score(IndexedSection section, HashSet<string> queryTerms, HashSet<string> requestedArticles)
    {
        var score = queryTerms.Count(term => section.BodyTerms.Contains(term)) * 2;

        // An explicit reference ("artículo 11") is a strong signal.
        const string prefix = "Artículo ";
        if (section.Section.Section.StartsWith(prefix, StringComparison.Ordinal)
            && requestedArticles.Contains(section.Section.Section[prefix.Length..]))
        {
            score += 10;
        }

        return score;
    }

    private static HashSet<string> RequestedArticleNumbers(string query)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var parts = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if ((parts[i] == "articulo" || parts[i] == "art") && parts[i + 1].All(char.IsAsciiDigit))
            {
                result.Add(parts[i + 1]);
            }
        }

        return result;
    }

    private static HashSet<string> Tokenize(string text)
    {
        var terms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.Length < 2 || StopWords.Contains(raw))
            {
                continue;
            }

            terms.Add(Stem(raw));
        }

        return terms;
    }

    /// <summary>Lowercases, strips diacritics and replaces punctuation with spaces.</summary>
    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return builder.ToString();
    }

    private static readonly string[] Enclitics = ["las", "los", "les", "nos", "la", "lo", "le", "se", "me"];

    private static readonly string[] VerbEndings = ["ando", "iendo", "able", "abl", "ado", "ido", "ar", "er", "ir", "an", "en", "a", "e", "o"];

    /// <summary>
    /// Light, deterministic Spanish reduction applied to both corpus and query terms: strips plurals, enclitic
    /// pronouns of infinitives ("consultarla" to "consultar") and common verb endings, so "pueden"/"puede" and
    /// "consultarla"/"consultar" share a stem. Stems keep at least four letters to avoid over-matching.
    /// </summary>
    private static string Stem(string term)
    {
        if (term.Length > 4 && term.EndsWith("es", StringComparison.Ordinal))
        {
            term = term[..^2];
        }
        else if (term.Length > 3 && term.EndsWith('s'))
        {
            term = term[..^1];
        }

        foreach (var enclitic in Enclitics)
        {
            if (term.EndsWith(enclitic, StringComparison.Ordinal) && term.Length - enclitic.Length >= 5)
            {
                var head = term[..^enclitic.Length];
                if (head.EndsWith("ar", StringComparison.Ordinal) || head.EndsWith("er", StringComparison.Ordinal)
                    || head.EndsWith("ir", StringComparison.Ordinal))
                {
                    term = head;
                    break;
                }
            }
        }

        foreach (var ending in VerbEndings)
        {
            if (term.EndsWith(ending, StringComparison.Ordinal) && term.Length - ending.Length >= 4)
            {
                return term[..^ending.Length];
            }
        }

        return term;
    }

    private sealed record IndexedSection(CorpusSection Section, HashSet<string> BodyTerms);
}
