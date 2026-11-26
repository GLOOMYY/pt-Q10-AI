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
    /// reached without a final answer the request ends with a 503 and the tokens already spent (R-RES-05). Defaults to 8.
    /// </summary>
    public int MaxLlmCalls { get; set; } = 8;
}
