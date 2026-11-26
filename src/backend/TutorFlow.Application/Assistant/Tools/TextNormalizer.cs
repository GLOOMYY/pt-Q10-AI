using System.Text;
using SecurityNormalizer = TutorFlow.Application.Security.Defenses.TextNormalizer;

namespace TutorFlow.Application.Assistant.Tools;

/// <summary>
/// Normalization used to match course and student names and article labels: the normalization of the defenses
/// (<see cref="SecurityNormalizer.Normalize"/>: no case, no accents, the <c>ñ</c> kept, single spaces) plus punctuation
/// turned into spaces, so <c>"¿Laura?"</c> matches <c>"laura"</c> and <c>"3.1"</c> becomes <c>"3 1"</c>.
/// </summary>
public static class TextNormalizer
{
    /// <summary>Normalizes a text for comparison.</summary>
    /// <param name="text">The text; <see langword="null"/> gives an empty string.</param>
    /// <returns>The normalized text, e.g. <c>"Sofía  Herrera"</c> → <c>"sofia herrera"</c>.</returns>
    public static string Normalize(string? text)
    {
        var normalized = SecurityNormalizer.Normalize(text);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '-' ? c : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>The words of a normalized text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The words.</returns>
    public static IReadOnlyList<string> Words(string? text) =>
        Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
