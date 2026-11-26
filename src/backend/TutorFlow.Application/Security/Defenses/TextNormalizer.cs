using System.Globalization;
using System.Text;

namespace TutorFlow.Application.Security.Defenses;

/// <summary>
/// Normalization used by every textual defense: no case, no accents, no repeated spaces (R-SEG-14). It also removes
/// the tricks a user could use to slip a name past a comparison: invisible characters, full-width letters and
/// look-alike hyphens.
/// </summary>
public static class TextNormalizer
{
    /// <summary>
    /// Lower-cases, decomposes (compatibility form, so full-width <c>ＥＳＴ</c> becomes <c>est</c>), drops the accents
    /// and invisible format characters, turns every dash into <c>-</c> and collapses runs of white space into one
    /// space. The <c>ñ</c> is kept: it is a different letter in Spanish, not an accent on <c>n</c> (escenarios/README §4.1).
    /// </summary>
    /// <param name="text">Any text; <see langword="null"/> gives an empty string.</param>
    /// <returns>The normalized text, trimmed.</returns>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var decomposed = text.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;

        foreach (var c in decomposed)
        {
            var category = char.GetUnicodeCategory(c);

            if (category == UnicodeCategory.NonSpacingMark)
            {
                // Keep the tilde of the ñ (n + U+0303); drop any other accent.
                if (c == '̃' && builder.Length > 0 && builder[^1] == 'n' && !pendingSpace)
                {
                    builder.Append(c);
                }

                continue;
            }

            if (category == UnicodeCategory.Format)
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(category == UnicodeCategory.DashPunctuation || c == '−'
                ? '-'
                : char.ToLowerInvariant(c));
        }

        // Re-compose so that "n" + U+0303 is one character again and compares equal across inputs.
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Whether <paramref name="term"/> appears in <paramref name="text"/> as a whole word: the characters just before and
    /// after the match are not letters or digits, so <c>est-002</c> is not found inside <c>est-0021</c>. Both arguments
    /// must already be normalized.
    /// </summary>
    /// <param name="text">Normalized text.</param>
    /// <param name="term">Normalized term, one or more words.</param>
    /// <returns><see langword="true"/> if the term is present as a whole word.</returns>
    public static bool ContainsWholeTerm(string text, string term)
    {
        if (term.Length == 0)
        {
            return false;
        }

        var start = 0;
        while (start <= text.Length - term.Length)
        {
            var index = text.IndexOf(term, start, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var end = index + term.Length;
            var after = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (before && after)
            {
                return true;
            }

            start = index + 1;
        }

        return false;
    }
}
