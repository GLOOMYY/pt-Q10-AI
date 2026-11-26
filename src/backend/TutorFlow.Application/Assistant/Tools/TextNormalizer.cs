using System.Globalization;
using System.Text;

namespace TutorFlow.Application.Assistant.Tools;

/// <summary>
/// Normalization used to match course and student names typed by the model or the user: lower case, no diacritics
/// (the <c>ñ</c> is kept, as in escenarios/README §4.1), punctuation as spaces and single spaces.
/// </summary>
public static class TextNormalizer
{
    /// <summary>Normalizes a text for comparison.</summary>
    /// <param name="text">The text; <see langword="null"/> gives an empty string.</param>
    /// <returns>The normalized text, e.g. <c>"Sofía  Herrera"</c> → <c>"sofia herrera"</c>.</returns>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                // Keep the tilde of ñ (n + U+0303), drop every other mark.
                if (c == '̃' && builder.Length > 0 && builder[^1] == 'n')
                {
                    builder[^1] = 'ñ';
                }

                continue;
            }

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
