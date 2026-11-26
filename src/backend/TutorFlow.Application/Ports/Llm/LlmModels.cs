using System.Text.Json;

namespace TutorFlow.Application.Ports.Llm;

/// <summary>Author of a message in the conversation sent to the model.</summary>
public enum LlmRole
{
    /// <summary>The user question.</summary>
    User = 0,

    /// <summary>A previous model turn (text and/or tool calls), echoed back within the same request loop.</summary>
    Model = 1,

    /// <summary>Results of the tools the model called in its previous turn.</summary>
    Tool = 2,
}

/// <summary>
/// A tool (function) the backend exposes to the model (R-ARQ-01). The model never receives the user identity as a
/// parameter: tools take it from the server session (R-SEG-06).
/// </summary>
/// <param name="Name">Function name, e.g. <c>rechazar</c> (R-ARQ-04).</param>
/// <param name="Description">What the tool does, for the model.</param>
/// <param name="ParametersSchema">JSON Schema (object) of the arguments. The adapter maps it to the provider format.</param>
public sealed record LlmToolDefinition(string Name, string Description, JsonElement ParametersSchema);

/// <summary>A tool call requested by the model.</summary>
/// <param name="Name">Name of the requested tool.</param>
/// <param name="Arguments">Arguments as a JSON object, exactly as the model produced them (untrusted input).</param>
/// <param name="Id">Provider call id, if the provider assigns one; echoed in <see cref="LlmToolResult.CallId"/>.</param>
/// <param name="ProviderState">Opaque value the adapter needs echoed back in the next request (e.g. Gemini thought
/// signatures). The application never reads or logs it.</param>
public sealed record LlmToolCall(string Name, JsonElement Arguments, string? Id = null, string? ProviderState = null);

/// <summary>The backend's answer to one tool call (R-ARQ-02 step 5: only authorized, already computed data).</summary>
/// <param name="Name">Name of the tool that was called.</param>
/// <param name="Response">Result as a JSON object.</param>
/// <param name="CallId">The <see cref="LlmToolCall.Id"/> being answered, if any.</param>
public sealed record LlmToolResult(string Name, JsonElement Response, string? CallId = null);

/// <summary>One message of the conversation. Use the factory methods to build valid combinations.</summary>
/// <param name="Role">Author.</param>
/// <param name="Text">Text, for <see cref="LlmRole.User"/> and optionally <see cref="LlmRole.Model"/>.</param>
/// <param name="ToolCalls">Tool calls of a <see cref="LlmRole.Model"/> turn; empty otherwise.</param>
/// <param name="ToolResults">Tool results of a <see cref="LlmRole.Tool"/> turn; empty otherwise.</param>
public sealed record LlmMessage(
    LlmRole Role,
    string? Text,
    IReadOnlyList<LlmToolCall> ToolCalls,
    IReadOnlyList<LlmToolResult> ToolResults)
{
    /// <summary>Builds the user message. There is a single one per request: no history (R-RES-07, D-12).</summary>
    /// <param name="text">The user question.</param>
    /// <returns>The message.</returns>
    public static LlmMessage User(string text) => new(LlmRole.User, text, [], []);

    /// <summary>Echoes a previous model turn (as returned in <see cref="LlmResponse"/>).</summary>
    /// <param name="text">Text of the turn, if any.</param>
    /// <param name="toolCalls">Tool calls of the turn, including their <see cref="LlmToolCall.ProviderState"/>.</param>
    /// <returns>The message.</returns>
    public static LlmMessage Model(string? text, IReadOnlyList<LlmToolCall> toolCalls) =>
        new(LlmRole.Model, text, toolCalls, []);

    /// <summary>Builds the message with the results of the tools the model called.</summary>
    /// <param name="results">One result per call.</param>
    /// <returns>The message.</returns>
    public static LlmMessage Tool(IReadOnlyList<LlmToolResult> results) => new(LlmRole.Tool, null, [], results);
}

/// <summary>A single call to the model.</summary>
/// <param name="SystemInstruction">System instructions. Never logged (R-LOG-05) nor revealed (R-SEG-13).</param>
/// <param name="Messages">The conversation of this request only (user question plus the tool loop).</param>
/// <param name="Tools">Tools the model may call (R-ARQ-01).</param>
/// <param name="Temperature">Sampling temperature; 0 for stable answers (R-QA-03).</param>
/// <param name="ResponseSchema">Optional JSON Schema the final text must follow (structured output, e.g. answer
/// plus proposed citations, R-ARQ-03). <see langword="null"/> for free text.</param>
public sealed record LlmRequest(
    string SystemInstruction,
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<LlmToolDefinition> Tools,
    double Temperature = 0,
    JsonElement? ResponseSchema = null);

/// <summary>
/// Token usage and cost of one call (R-RES-05). Field names follow Gemini's <c>usageMetadata</c>; another provider's
/// adapter maps its own counters to these concepts.
/// </summary>
/// <param name="PromptTokens"><c>promptTokenCount</c>.</param>
/// <param name="CandidatesTokens"><c>candidatesTokenCount</c>.</param>
/// <param name="ThoughtsTokens"><c>thoughtsTokenCount</c> (reasoning tokens, billed as output).</param>
/// <param name="TotalTokens"><c>totalTokenCount</c>; the response <c>tokens</c> is the sum of this value over all
/// calls of the request.</param>
/// <param name="CostUsd">Cost of this call, <b>not rounded</b>:
/// <c>(PromptTokens × input price + (CandidatesTokens + ThoughtsTokens) × output price) / 1 000 000</c>, with the
/// configured prices (R-DEP-02); computed with <see cref="LlmCost.Compute"/>. The use case sums the calls and rounds the total to 6 decimals.</param>
public sealed record LlmUsage(int PromptTokens, int CandidatesTokens, int ThoughtsTokens, int TotalTokens, decimal CostUsd)
{
    /// <summary>No usage (no call made).</summary>
    public static LlmUsage None { get; } = new(0, 0, 0, 0, 0m);
}

/// <summary>The model's reply to one <see cref="LlmRequest"/>.</summary>
/// <param name="Text">Text of the reply, if any. Untrusted: citations it proposes are validated (R-ARQ-03).</param>
/// <param name="ToolCalls">Tool calls requested by the model; empty if the reply is final.</param>
/// <param name="Usage">Tokens and cost of this call (R-RES-05).</param>
public sealed record LlmResponse(string? Text, IReadOnlyList<LlmToolCall> ToolCalls, LlmUsage Usage);
