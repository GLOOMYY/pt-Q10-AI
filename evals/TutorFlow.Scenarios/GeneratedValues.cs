using System.Text;

namespace TutorFlow.Scenarios;

/// <summary>
/// Resolves the <c>&lt;generar: …&gt;</c> markers of <c>pregunta</c> exactly as README section 3.3 says, without
/// interpreting them: the marker must be the known text of that table for that scenario, otherwise it is an error
/// (the scenario fails). Lengths are in characters (all the text is in the BMP, so one char is one code point).
/// </summary>
public static class GeneratedValues
{
    private const string Prefix = "<generar:";
    private const int Length = 1000;

    // BASE = "¿Cuál es la nota mínima para aprobar una asignatura?" (52 characters, NFC), written with escapes so that
    // no editor can decompose it.
    private const string Base = "¿Cuál es la nota mínima para aprobar una asignatura?";

    private static readonly Dictionary<string, (string Marker, Func<string> Text)> Table = new(StringComparer.Ordinal)
    {
        ["ESC-ENT-03"] = (
            "<generar: '¿Cuál es la nota mínima para aprobar una asignatura?' + ' Gracias.' repetido hasta completar exactamente 1000 caracteres>",
            T1000),
        ["ESC-ENT-04"] = (
            "<generar: el texto de ESC-ENT-03 + 'X' (1001 caracteres)>",
            () => T1000() + "X"),
        ["ESC-ENT-05"] = (
            "<generar: '   ' + texto de ESC-ENT-03 + '   '>",
            () => "   " + T1000() + "   "),
        ["ESC-ENT-06"] = (
            "<generar: 'ñáéíóú' repetido hasta 1000 caracteres>",
            () => Repeat("ñáéíóú", string.Empty)),
    };

    /// <summary>Whether a <c>pregunta</c> value is (or contains) a generation marker.</summary>
    /// <param name="text">The <c>pregunta</c> value.</param>
    /// <returns><see langword="true"/> if it holds a <c>&lt;generar:</c> marker.</returns>
    public static bool IsMarker(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Contains(Prefix, StringComparison.Ordinal);
    }

    /// <summary>Replaces the marker with the exact text of README section 3.3.</summary>
    /// <param name="scenarioId">Id of the scenario the marker comes from.</param>
    /// <param name="marker">The <c>pregunta</c> value, which must be exactly the known marker of that scenario.</param>
    /// <returns>The generated text (NFC).</returns>
    /// <exception cref="InvalidOperationException">Unknown marker, or a marker in a scenario that has none in the table.</exception>
    public static string Resolve(string scenarioId, string marker)
    {
        ArgumentNullException.ThrowIfNull(scenarioId);
        ArgumentNullException.ThrowIfNull(marker);
        if (!Table.TryGetValue(scenarioId, out var known)
            || !string.Equals(known.Marker.Normalize(NormalizationForm.FormC), marker.Normalize(NormalizationForm.FormC), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unknown generation marker in {scenarioId}: {marker}");
        }

        return known.Text().Normalize(NormalizationForm.FormC);
    }

    // T1000 = BASE followed by " Gracias." repeated and truncated to 1000 characters (BASE + 105 repetitions + " Gr").
    private static string T1000() => Repeat(" Gracias.", Base);

    // "Truncate to N" keeps the first N characters of the repetition.
    private static string Repeat(string repeated, string head)
    {
        var builder = new StringBuilder(head);
        while (builder.Length < Length)
        {
            builder.Append(repeated);
        }

        return builder.ToString(0, Length);
    }
}
