using System.Text;
using System.Text.Json;
using TutorFlow.Application.Ports.Llm;

namespace TutorFlow.Infrastructure.Llm;

/// <summary>Maps a Gemini <c>generateContent</c> reply to an <see cref="LlmResponse"/> (R-RES-05).</summary>
internal static class GeminiResponseMapper
{
    /// <summary>Parses the reply body.</summary>
    /// <param name="json">Response body.</param>
    /// <param name="inputPrice">USD per million input tokens.</param>
    /// <param name="outputPrice">USD per million output tokens.</param>
    /// <returns>Text, tool calls and usage.</returns>
    /// <exception cref="InvalidOperationException">The reply has neither text nor tool calls.</exception>
    /// <exception cref="JsonException">The body is not valid JSON.</exception>
    public static LlmResponse Parse(string json, decimal inputPrice, decimal outputPrice)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var usage = ParseUsage(root, inputPrice, outputPrice);

        var text = new StringBuilder();
        var calls = new List<LlmToolCall>();
        if (root.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array
            && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts))
        {
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("functionCall", out var call))
                {
                    using var emptyArgs = JsonDocument.Parse("{}");
                    var args = call.TryGetProperty("args", out var a) ? a.Clone() : emptyArgs.RootElement.Clone();
                    calls.Add(new LlmToolCall(
                        call.GetProperty("name").GetString() ?? throw new InvalidOperationException("Unnamed function call."),
                        args,
                        call.TryGetProperty("id", out var id) ? id.GetString() : null,
                        part.TryGetProperty("thoughtSignature", out var sig) ? sig.GetString() : null));
                }
                else if (part.TryGetProperty("text", out var t)
                    && !(part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
                {
                    text.Append(t.GetString());
                }
            }
        }

        if (text.Length == 0 && calls.Count == 0)
        {
            throw new InvalidOperationException("The reply has neither text nor function calls.");
        }

        return new LlmResponse(text.Length == 0 ? null : text.ToString(), calls, usage);
    }

    private static LlmUsage ParseUsage(JsonElement root, decimal inputPrice, decimal outputPrice)
    {
        if (!root.TryGetProperty("usageMetadata", out var u))
        {
            return LlmUsage.None;
        }

        var prompt = Int(u, "promptTokenCount");
        var candidates = Int(u, "candidatesTokenCount");
        var thoughts = Int(u, "thoughtsTokenCount"); // May be missing: 0.
        var total = u.TryGetProperty("totalTokenCount", out var t) && t.ValueKind == JsonValueKind.Number
            ? t.GetInt32()
            : prompt + candidates + thoughts;
        var cost = LlmCost.Compute(prompt, candidates, thoughts, inputPrice, outputPrice);
        return new LlmUsage(prompt, candidates, thoughts, total, cost);
    }

    private static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
}
