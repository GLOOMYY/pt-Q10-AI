namespace TutorFlow.Infrastructure.Knowledge;

/// <summary>Settings of the knowledge base (the corpus location).</summary>
public sealed class KnowledgeOptions
{
    /// <summary>Environment variable that overrides <see cref="CorpusPath"/>.</summary>
    public const string CorpusPathVariable = "KNOWLEDGE_CORPUS_PATH";

    /// <summary>
    /// Directory holding the corpus files (R-FUE-01). When empty, the loader probes
    /// <c>material/reglamento</c> next to the application binaries (Docker: <c>/app/material/reglamento</c>) and then in
    /// every parent directory (repository checkout, tests).
    /// </summary>
    public string? CorpusPath { get; set; }
}
