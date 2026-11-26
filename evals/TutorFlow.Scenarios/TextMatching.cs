using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TutorFlow.Scenarios;

/// <summary>
/// Comparison rules for <c>respuesta.contiene</c>, <c>respuesta.noContiene</c> and <c>log.noContiene</c>
/// (escenarios/README section 4.1): no case and no accents (the <c>ñ</c> is kept), and numeric boundaries.
/// </summary>
public static class TextMatching
{
    /// <summary>
    /// Lower-cases, decomposes to NFD and removes the diacritical marks (Unicode category <c>Mn</c>) except the tilde of
    /// the <c>ñ</c>, so <c>"año"</c> stays different from <c>"ano"</c>. The result is recomposed to NFC.
    /// </summary>
    /// <param name="text">Text to normalize.</param>
    /// <returns>The normalized text.</returns>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                // Only the virgulilla over an 'n' survives: that is the 'ñ'.
                if (ch == '̃' && builder.Length > 0 && builder[^1] == 'n')
                {
                    builder.Append(ch);
                }

                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Whether <paramref name="text"/> contains <paramref name="fragment"/> under the comparison rules. A fragment that
    /// starts or ends with a digit only matches on a numeric boundary: not preceded by a digit or by "digit and point",
    /// and not followed by a digit or by "point and digit" (<c>(?&lt;!\d)(?&lt;!\d\.)</c> + fragment + <c>(?!\d)(?!\.\d)</c>,
    /// each side only if that end of the fragment is a digit). The decimal separator is the point only.
    /// </summary>
    /// <param name="text">Text to search in (e.g. the answer).</param>
    /// <param name="fragment">Fragment to find.</param>
    /// <returns><see langword="true"/> if it appears.</returns>
    public static bool Contains(string text, string fragment)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fragment);
        var haystack = Normalize(text);
        var needle = Normalize(fragment);
        if (needle.Length == 0)
        {
            return true;
        }

        var pattern = new StringBuilder();
        if (IsDigit(needle[0]))
        {
            pattern.Append(@"(?<![0-9])(?<![0-9]\.)");
        }

        pattern.Append(Regex.Escape(needle));
        if (IsDigit(needle[^1]))
        {
            pattern.Append(@"(?![0-9])(?!\.[0-9])");
        }

        return Regex.IsMatch(haystack, pattern.ToString(), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
    }

    private static bool IsDigit(char ch) => ch is >= '0' and <= '9';
}
