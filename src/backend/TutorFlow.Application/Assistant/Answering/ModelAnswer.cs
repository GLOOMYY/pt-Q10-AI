using System.Text.Json;

namespace TutorFlow.Application.Assistant.Answering;

/// <summary>
/// The final answer the model proposes (R-ARQ-03): text plus citations, both untrusted until validated by
/// <see cref="CitationValidator"/>.
/// </summary>
/// <param name="Text">Proposed <c>respuesta</c>.</param>
/// <param name="Citations">Proposed <c>citas</c>, in the order the model gave them.</param>
public sealed record ModelAnswer(string Text, IReadOnlyList<Citation> Citations)
{
    /// <summary>
    /// JSON Schema of the structured final answer requested from the model:
    /// <c>{ "respuesta": string, "citas": [{ "documento": string, "seccion": string }] }</c>.
    /// </summary>
    public static JsonElement Schema { get; } = ParseSchema(
        """
        {"type":"object","properties":{
          "respuesta":{"type":"string"},
          "citas":{"type":"array","items":{"type":"object","properties":{
            "documento":{"type":"string"},"seccion":{"type":"string"}},"required":["documento","seccion"]}}},
         "required":["respuesta","citas"]}
        """);

    /// <summary>
    /// Reads the final text of the model. The structured JSON (optionally inside a Markdown code fence) is preferred;
    /// a plain text reply is taken as the answer without citations.
    /// </summary>
    /// <param name="text">The final text of the model.</param>
    /// <returns>The proposed answer, or <see langword="null"/> if the reply has no usable text.</returns>
    public static ModelAnswer? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = StripCodeFence(text.Trim());
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("respuesta", out var answer)
                    && answer.ValueKind == JsonValueKind.String)
                {
                    var value = answer.GetString();
                    return string.IsNullOrWhiteSpace(value) ? null : new ModelAnswer(value.Trim(), ReadCitations(root));
                }
            }
            catch (JsonException)
            {
                // Not the structured answer: fall back to plain text below.
            }
        }

        return new ModelAnswer(text.Trim(), []);
    }

    private static List<Citation> ReadCitations(JsonElement root)
    {
        var citations = new List<Citation>();
        if (!root.TryGetProperty("citas", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return citations;
        }

        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("documento", out var document) && document.ValueKind == JsonValueKind.String
                && item.TryGetProperty("seccion", out var section) && section.ValueKind == JsonValueKind.String)
            {
                citations.Add(new Citation(document.GetString()!, section.GetString()!));
            }
        }

        return citations;
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstLineEnd = text.IndexOf('\n', StringComparison.Ordinal);
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
        return firstLineEnd > 0 && lastFence > firstLineEnd ? text[(firstLineEnd + 1)..lastFence].Trim() : text;
    }

    private static JsonElement ParseSchema(string schema)
    {
        using var document = JsonDocument.Parse(schema);
        return document.RootElement.Clone();
    }
}
