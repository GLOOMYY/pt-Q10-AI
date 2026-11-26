namespace TutorFlow.Application.Assistant;

/// <summary>Settings of the assistant use case.</summary>
public sealed class AssistantOptions
{
    /// <summary>
    /// Maximum number of LLM calls for one question (the tool loop). It bounds the cost of a single request; when it is
    /// reached without a final answer the request ends with a 503 and the tokens already spent (R-RES-05). Defaults to 8.
    /// </summary>
    public int MaxLlmCalls { get; set; } = 8;
}
