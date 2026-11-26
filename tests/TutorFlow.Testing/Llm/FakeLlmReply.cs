using System.Text.Json;
using TutorFlow.Application.Ports.Llm;

namespace TutorFlow.Testing.Llm;

/// <summary>
/// One scripted reply of the <see cref="FakeLlmClient"/>: text and/or tool calls plus the token counts it reports.
/// Build it with the factory methods.
/// </summary>
public sealed record FakeLlmReply
{
    /// <summary>Name of the rejection tool (R-ARQ-04).</summary>
    public const string RejectToolName = "rechazar";

    /// <summary>Default prompt tokens reported by a reply.</summary>
    public const int DefaultPromptTokens = 100;

    /// <summary>Default candidate tokens reported by a reply.</summary>
    public const int DefaultCandidatesTokens = 20;

    /// <summary>Text of the reply, if any.</summary>
    public string? Text { get; init; }

    /// <summary>Tool calls of the reply, in order.</summary>
    public IReadOnlyList<LlmToolCall> ToolCalls { get; init; } = [];

    /// <summary>Reported <c>promptTokenCount</c>.</summary>
    public int PromptTokens { get; init; } = DefaultPromptTokens;

    /// <summary>Reported <c>candidatesTokenCount</c>.</summary>
    public int CandidatesTokens { get; init; } = DefaultCandidatesTokens;

    /// <summary>Reported <c>thoughtsTokenCount</c>.</summary>
    public int ThoughtsTokens { get; init; }

    /// <summary>A final text reply (for structured output, pass the JSON as text).</summary>
    /// <param name="text">The text.</param>
    /// <returns>The reply.</returns>
    public static FakeLlmReply FromText(string text) => new() { Text = text };

    /// <summary>
    /// A reply that calls tools. D-32: <c>rechazar</c> is always decided before any data tool, so it must come first.
    /// </summary>
    /// <param name="calls">The calls, in order.</param>
    /// <returns>The reply.</returns>
    /// <exception cref="ArgumentException"><c>rechazar</c> appears after a data tool.</exception>
    public static FakeLlmReply FromToolCalls(params LlmToolCall[] calls)
    {
        ArgumentNullException.ThrowIfNull(calls);
        var rejectIndex = Array.FindIndex(calls, c => c.Name == RejectToolName);
        if (rejectIndex > 0)
        {
            throw new ArgumentException("D-32: 'rechazar' must be called before any data tool.", nameof(calls));
        }

        return new FakeLlmReply { ToolCalls = calls };
    }

    /// <summary>A reply that only calls <c>rechazar(motivo)</c> (R-ARQ-04, D-29).</summary>
    /// <param name="reason">The rejection reason.</param>
    /// <returns>The reply.</returns>
    public static FakeLlmReply Reject(string reason) =>
        FromToolCalls(Call(RejectToolName, new { motivo = reason }));

    /// <summary>Builds a tool call with arguments serialized from an anonymous object or dictionary.</summary>
    /// <param name="name">Tool name.</param>
    /// <param name="arguments">Arguments object; <see langword="null"/> for none.</param>
    /// <param name="id">Optional provider call id.</param>
    /// <returns>The tool call.</returns>
    public static LlmToolCall Call(string name, object? arguments = null, string? id = null) =>
        new(name, JsonSerializer.SerializeToElement(arguments ?? new { }), id);

    /// <summary>Returns a copy with explicit token counts.</summary>
    /// <param name="promptTokens">Prompt tokens.</param>
    /// <param name="candidatesTokens">Candidate tokens.</param>
    /// <param name="thoughtsTokens">Reasoning tokens.</param>
    /// <returns>The reply with the given usage.</returns>
    public FakeLlmReply WithTokens(int promptTokens, int candidatesTokens, int thoughtsTokens = 0) =>
        this with { PromptTokens = promptTokens, CandidatesTokens = candidatesTokens, ThoughtsTokens = thoughtsTokens };
}
