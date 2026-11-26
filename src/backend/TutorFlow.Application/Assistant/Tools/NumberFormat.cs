using System.Globalization;

namespace TutorFlow.Application.Assistant.Tools;

/// <summary>
/// Number formats shown to the user (SPEC §6.8 "Formato numérico"). Tools hand the model numbers already formatted, so
/// the answer copies them as they must be seen: grades with 1 decimal (<c>4.0</c>, never <c>4</c>; API-06), averages
/// and accumulated grades with 2 (<c>4.39</c>, <c>2.40</c>; R-CAL-04, R-QA-05), and cost with 6 (R-RES-05).
/// </summary>
public static class NumberFormat
{
    /// <summary>A grade with exactly 1 decimal, half away from zero (D-06).</summary>
    /// <param name="value">The grade.</param>
    /// <returns>E.g. <c>4.0</c>, <c>4.5</c>.</returns>
    public static string Grade(decimal value) =>
        Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>An average or accumulated grade with exactly 2 decimals, half away from zero (D-06).</summary>
    /// <param name="value">The value.</param>
    /// <returns>E.g. <c>4.39</c>, <c>2.40</c>.</returns>
    public static string Average(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>A cost in USD with exactly 6 decimals, half away from zero (R-RES-05).</summary>
    /// <param name="value">The cost.</param>
    /// <returns>E.g. <c>0.000123</c>.</returns>
    public static string Cost(decimal value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero).ToString("0.000000", CultureInfo.InvariantCulture);

    /// <summary>A percentage with up to 2 decimals and the <c>%</c> sign, e.g. <c>18.75 %</c> or <c>60 %</c>.</summary>
    /// <param name="value">The percentage.</param>
    /// <returns>The text.</returns>
    public static string Percent(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture) + " %";
}
