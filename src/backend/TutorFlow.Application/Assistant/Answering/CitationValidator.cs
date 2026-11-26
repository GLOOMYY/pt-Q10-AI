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
/// <item><description>A rule the consulted data really involve (<see cref="ToolRunState.BackedRules"/>) is added when
/// the answer applies it without citing it (R-RES-02: data + rule).</description></item>
/// <item><description>An answer that cites Evaluación "Sección 3" and states the weights of the cuts also cites
/// "Sección 2", where they are defined (R-RES-01, R-CAL-01).</description></item>
/// </list>
/// </summary>
public sealed partial class CitationValidator
{
    /// <summary>Answer when the information is not in the documents nor in the data (R-RES-03, CL-14, CL-18).</summary>
    public const string NoInformationText = "No encuentro esa información en los documentos disponibles.";

    private const string EvaluationDocument = "evaluacion_y_calificaciones.md";
    private const string RegulationDocument = "reglamento_academico_2025.md";

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

        RemoveOffTopicAbsences(valid, state);
        RemoveOffTopicNonHabilitable(valid, state);
        AddBackedRules(TextNormalizer.Normalize(proposed.Text), valid, state);
        AddCutWeights(proposed.Text, valid);
        return new ValidatedAnswer(proposed.Text, valid);
    }

    /// <summary>
    /// R-RES-01, ESC-BAN-04 (DUDA-44): a general question about how the definitive grade is computed (the answer cites
    /// Evaluación "Sección 3", no student data were read and the question does not mention attendance) does not ask
    /// about the loss by absences, so an "Artículo 11" citation the model added is removed. Only the citation list
    /// changes: the text written by the model is never edited (R-ARQ-03). The rule cannot add anything, so it can never
    /// cite a rule that does not support the answer.
    /// </summary>
    private static void RemoveOffTopicAbsences(List<Citation> valid, ToolRunState state)
    {
        var absences = new Citation(RegulationDocument, "Artículo 11");
        if (state.UsedStudentData
            || !valid.Contains(absences)
            || !valid.Contains(new Citation(EvaluationDocument, "Sección 3"))
            || AttendancePattern().IsMatch(TextNormalizer.Normalize(state.Question)))
        {
            return;
        }

        valid.Remove(absences);
    }

    /// <summary>
    /// R-RES-01, ESC-CIT-01: a general question about habilitation ("¿Desde qué nota puedo habilitar una asignatura?":
    /// the answer cites "Artículo 6", no student data were read and the question does not ask about the courses that
    /// cannot be habilitated) is answered by Artículo 6 alone, so an "Artículo 7" citation the model added is removed.
    /// Same criterion as <see cref="RemoveOffTopicAbsences"/> (DUDA-44): only the citation list changes, the text written
    /// by the model is never edited (R-ARQ-03), and the rule can only remove, never add, a citation. With student data
    /// Artículo 7 is kept: a lost course of the data may be non-habilitable (StudentReport asks for it then).
    /// </summary>
    private static void RemoveOffTopicNonHabilitable(List<Citation> valid, ToolRunState state)
    {
        var nonHabilitable = new Citation(RegulationDocument, "Artículo 7");
        if (state.UsedStudentData
            || !valid.Contains(nonHabilitable)
            || !valid.Contains(new Citation(RegulationDocument, "Artículo 6"))
            || NonHabilitableTopicPattern().IsMatch(TextNormalizer.Normalize(state.Question)))
        {
            return;
        }

        valid.Remove(nonHabilitable);
    }

    /// <summary>
    /// R-RES-02 (data + rule): a rule that the consulted data really involve (e.g. Artículo 11 for a course lost by
    /// absences) is cited when the answer applies it and the model forgot its citation (ESC-COO-01).
    /// </summary>
    private void AddBackedRules(string normalizedText, List<Citation> valid, ToolRunState state)
    {
        foreach (var rule in state.BackedRules)
        {
            if (!valid.Contains(rule.Citation)
                && _knowledge.IsCitable(rule.Citation)
                && Regex.IsMatch(normalizedText, $@"\b{Regex.Escape(rule.Trigger)}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            {
                valid.Add(rule.Citation);
            }
        }
    }

    /// <summary>
    /// R-RES-01, R-CAL-01 (ESC-BAN-04): the definitive grade of Sección 3 is computed "según sus pesos", and the weights
    /// of the cuts (30 %, 30 %, 40 %) are defined in Sección 2. An answer that cites Sección 3 and states those weights
    /// also cites Sección 2, just before Sección 3 (document order).
    /// </summary>
    private void AddCutWeights(string text, List<Citation> valid)
    {
        var formula = new Citation(EvaluationDocument, "Sección 3");
        var weights = new Citation(EvaluationDocument, "Sección 2");
        var index = valid.IndexOf(formula);
        if (index < 0
            || valid.Contains(weights)
            || !_knowledge.IsCitable(weights)
            || !Weight30Pattern().IsMatch(text)
            || !Weight40Pattern().IsMatch(text))
        {
            return;
        }

        valid.Insert(index, weights);
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

    [GeneratedRegex(@"(?<![0-9.,])30\s*(%|por\s*ciento)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex Weight30Pattern();

    [GeneratedRegex(@"(?<![0-9.,])40\s*(%|por\s*ciento)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex Weight40Pattern();

    // Normalized words about attendance in the QUESTION (never used on the answer): inasistencia(s), asistencia,
    // falta(s), faltar, falla(s), ausencia(s).
    [GeneratedRegex(@"\b(inasistencia|asistencia|falt|falla|ausencia)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex AttendancePattern();

    // Normalized words of a QUESTION that asks about the courses that cannot be habilitated (Artículo 7): a negation
    // ("no se puede habilitar", "no habilitable"), "cuáles/qué asignaturas", "cualquier/todas", exceptions, practicums,
    // degree projects or attendance. Short words need a closing boundary ("no" must not match "nota").
    [GeneratedRegex(@"\b(no|ninguna|nunca|cualquier|todas?)\b|\b(cuales|que)\s+(asignaturas|materias)\b|\b(excep|exclu|restric|practica|pasantia|trabajo\s+de\s+grado|tesis|inasistencia|asistencia|falt|falla|ausencia)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex NonHabilitableTopicPattern();
}
