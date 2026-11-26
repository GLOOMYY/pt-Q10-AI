using System.Text.RegularExpressions;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Ports.Knowledge;

namespace TutorFlow.Application.Assistant.Answering;

/// <summary>An answer whose citations were validated (R-ARQ-03).</summary>
/// <param name="Text">The <c>respuesta</c> to return.</param>
/// <param name="Citations">The valid citations, without duplicates, in order of use.</param>
public sealed record ValidatedAnswer(string Text, IReadOnlyList<Citation> Citations);

/// <summary>
/// Validates the citations the model proposes before the answer is returned (R-ARQ-03, R-FUE-02, CL-18):
/// <list type="bullet">
/// <item><description>Document citations must exist literally in the index (<see cref="IKnowledgeBase.IsCitable"/>);
/// the others are discarded.</description></item>
/// <item><description>Data citations must have the exact format of R-RES-02 with the period of <c>GET /salud</c>, and
/// may only refer to data the tools really used ("Datos del estudiante" for a student, "Datos del programa" of each
/// program used for a coordinator).</description></item>
/// <item><description>Duplicates are removed, keeping the first occurrence (R-RES-01).</description></item>
/// <item><description>If the answer depended on a discarded citation, it becomes "No encuentro esa información en los
/// documentos disponibles." with no citations (CL-18, R-RES-03).</description></item>
/// <item><description>If data were used and the model proposed no valid data citation, the backend adds the data
/// citations of the data used (R-RES-02).</description></item>
/// </list>
/// </summary>
public sealed partial class CitationValidator
{
    /// <summary>Answer when the information is not in the documents nor in the data (R-RES-03, CL-14, CL-18).</summary>
    public const string NoInformationText = "No encuentro esa información en los documentos disponibles.";

    private readonly IKnowledgeBase _knowledge;

    /// <summary>Creates the validator.</summary>
    /// <param name="knowledge">The knowledge base with the index of articles and sections.</param>
    public CitationValidator(IKnowledgeBase knowledge) => _knowledge = knowledge;

    /// <summary>Validates the proposed answer.</summary>
    /// <param name="proposed">The answer proposed by the model.</param>
    /// <param name="state">What the tools of the request used.</param>
    /// <returns>The answer with only valid citations.</returns>
    public ValidatedAnswer Validate(ModelAnswer proposed, ToolRunState state)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(state);

        var allowedData = DataCitations.Allowed(state);
        var valid = new List<Citation>();
        var discarded = new List<Citation>();
        foreach (var citation in proposed.Citations)
        {
            var isData = citation.Document is DataCitations.StudentDocument or DataCitations.ProgramDocument;
            var ok = isData ? allowedData.Contains(citation) : _knowledge.IsCitable(citation);
            if (!ok)
            {
                discarded.Add(citation);
            }
            else if (!valid.Contains(citation))
            {
                valid.Add(citation);
            }
        }

        // R-RES-03: an answer that says it has no information never carries a "filler" citation.
        if (SaysNoInformation(proposed.Text))
        {
            return new ValidatedAnswer(proposed.Text, []);
        }

        if (DependedOnDiscarded(proposed, valid, discarded, state))
        {
            return new ValidatedAnswer(NoInformationText, []);
        }

        // R-RES-02: data used to answer always carries its data citation.
        if (state.UsedStudentData && !valid.Any(c => allowedData.Contains(c)))
        {
            valid.AddRange(allowedData);
        }

        return new ValidatedAnswer(proposed.Text, valid);
    }

    /// <summary>
    /// The answer depended on a discarded document citation when nothing valid supports it (no valid citation and no
    /// data used), or when its text mentions the discarded article or section and no valid citation has that label
    /// (e.g. "el artículo 8 dice…" citing the non-existent Artículo 8, CL-18).
    /// </summary>
    private static bool DependedOnDiscarded(
        ModelAnswer proposed, List<Citation> valid, List<Citation> discarded, ToolRunState state)
    {
        var discardedDocuments = discarded
            .Where(c => c.Document is not DataCitations.StudentDocument and not DataCitations.ProgramDocument)
            .ToList();
        if (discardedDocuments.Count == 0)
        {
            return false;
        }

        if (valid.Count == 0 && !state.UsedStudentData)
        {
            return true;
        }

        var text = TextNormalizer.Normalize(proposed.Text);
        var validLabels = valid.Select(c => c.Section).ToHashSet(StringComparer.Ordinal);
        return discardedDocuments
            .Where(c => !validLabels.Contains(c.Section))
            .Any(c => Mentions(text, c.Section));
    }

    private static bool Mentions(string normalizedText, string section)
    {
        var article = ArticlePattern().Match(section);
        if (article.Success)
        {
            return Regex.IsMatch(
                normalizedText,
                $@"\b(articulo|art)\s*{Regex.Escape(article.Groups[1].Value)}(?![0-9])",
                RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        }

        var sectionMatch = SectionPattern().Match(section);
        if (sectionMatch.Success)
        {
            // The normalizer turns "3.1" into "3 1".
            var number = Regex.Escape(sectionMatch.Groups[1].Value).Replace(@"\.", " ", StringComparison.Ordinal);
            return Regex.IsMatch(
                normalizedText,
                $@"\bseccion\s*{number}(?![0-9])",
                RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        }

        return false;
    }

    private static bool SaysNoInformation(string text) =>
        TextNormalizer.Normalize(text).StartsWith(TextNormalizer.Normalize(NoInformationText), StringComparison.Ordinal);

    [GeneratedRegex(@"^Art[ií]culo\s+([0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ArticlePattern();

    [GeneratedRegex(@"^Secci[oó]n\s+([0-9]+(?:\.[0-9]+)?)$", RegexOptions.CultureInvariant)]
    private static partial Regex SectionPattern();
}
