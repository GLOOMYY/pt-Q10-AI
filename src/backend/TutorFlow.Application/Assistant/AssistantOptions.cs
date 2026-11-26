namespace TutorFlow.Application.Assistant;

/// <summary>
/// Settings of the assistant use case, bound from the configuration section <see cref="Section"/> (AGENTS §7). No
/// variable is required: without configuration the defaults apply.
/// </summary>
public sealed class AssistantOptions
{
    /// <summary>Configuration section; as an environment variable, <c>Assistant__MaxLlmCalls</c>.</summary>
    public const string Section = "Assistant";

    /// <summary>
    /// Maximum number of LLM calls for one question (the tool loop). It bounds the cost of a single request; when it is
    /// reached without a final answer the request ends with a 503 and the tokens already spent (R-RES-05). Defaults to
    /// <see cref="DefaultMaxLlmCalls"/>.
    /// </summary>
    /// <remarks>
    /// DUDA-29 (answer of the owner): 4 calls, so as not to raise the cost too much. The usual routes fit: a data or
    /// search tool, an optional second round of tools (e.g. <c>buscar_estudiante</c> → data, or data →
    /// <c>buscar_reglamento</c>) and the final answer. The last allowed call tells the model to answer with what it has.
    /// </remarks>
    public int MaxLlmCalls { get; set; } = DefaultMaxLlmCalls;

    /// <summary>Default of <see cref="MaxLlmCalls"/> (DUDA-29).</summary>
    public const int DefaultMaxLlmCalls = 4;
}
