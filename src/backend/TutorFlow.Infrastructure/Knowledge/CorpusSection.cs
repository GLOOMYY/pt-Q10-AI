namespace TutorFlow.Infrastructure.Knowledge;

/// <summary>One citable unit of the corpus.</summary>
/// <param name="Document">File name of the document.</param>
/// <param name="Section">Exact citable label (<c>Artículo 5</c>, <c>Sección 3.1</c>).</param>
/// <param name="Text">Text of the section, a heading and its body.</param>
public sealed record CorpusSection(string Document, string Section, string Text);
