namespace TutorFlow.Infrastructure.Knowledge;

/// <summary>How the sections of a corpus document are cited (R-RES-01).</summary>
public enum CorpusDocumentKind
{
    /// <summary>Statute split by <c>**Artículo N. Title.**</c> paragraphs; cited as <c>Artículo N</c>.</summary>
    Articles = 0,

    /// <summary>Document split by numbered headings (<c>## N.</c> / <c>### N.M</c>); cited as <c>Sección N[.M]</c>.</summary>
    NumberedSections = 1,
}

/// <summary>A document the assistant may cite.</summary>
/// <param name="FileName">File name, used both to load the file and as <c>documento</c> of the citation.</param>
/// <param name="Kind">How the file is split into citable sections.</param>
public sealed record CorpusDocument(string FileName, CorpusDocumentKind Kind);

/// <summary>
/// The closed list of documents that are indexed (R-FUE-01). <c>reglamento_academico_2023.md</c> is deliberately absent
/// (D-01: derogated by Article 3 of the 2025 statute) and so is the Bienestar annex, which the parser skips (D-07).
/// </summary>
public static class CorpusDocuments
{
    /// <summary>The indexed documents.</summary>
    public static IReadOnlyList<CorpusDocument> Indexed { get; } =
    [
        new("reglamento_academico_2025.md", CorpusDocumentKind.Articles),
        new("evaluacion_y_calificaciones.md", CorpusDocumentKind.NumberedSections),
        new("bienestar_y_becas.md", CorpusDocumentKind.NumberedSections),
    ];
}
