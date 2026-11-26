namespace TutorFlow.Domain.Grading;

/// <summary>
/// Constants of the grading scheme (SPEC glossary, R-CAL). Every grade is a <see cref="decimal"/>: never
/// <see cref="double"/> or <see cref="float"/> (R-CAL-02, D-06).
/// </summary>
public static class GradingRules
{
    /// <summary>Weight of cut 1 (30 %, Evaluación §2).</summary>
    public const decimal Cut1Weight = 0.3m;

    /// <summary>Weight of cut 2 (30 %, Evaluación §2).</summary>
    public const decimal Cut2Weight = 0.3m;

    /// <summary>Weight of cut 3 (40 %, Evaluación §2).</summary>
    public const decimal Cut3Weight = 0.4m;

    /// <summary>Lowest grade of the scale (Evaluación §1).</summary>
    public const decimal MinGrade = 0.0m;

    /// <summary>Highest grade of the scale (Evaluación §1).</summary>
    public const decimal MaxGrade = 5.0m;

    /// <summary>Minimum final grade to pass a course (R-CAL-07, Art. 5): exactly 3.0 passes.</summary>
    public const decimal PassingGrade = 3.0m;

    /// <summary>Half of the 1-decimal rounding step: a value rounds up to the target from <c>target - 0.05</c> (R-CAL-11).</summary>
    public const decimal HalfRoundingStep = 0.05m;

    /// <summary>
    /// Rounds a grade to 1 decimal, midpoint away from zero (R-CAL-01, R-CAL-03, D-06).
    /// The explicit <see cref="MidpointRounding.AwayFromZero"/> is mandatory: .NET defaults to banker's rounding.
    /// </summary>
    /// <param name="value">The unrounded grade.</param>
    /// <returns>The grade with 1 decimal.</returns>
    public static decimal RoundGrade(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    /// <summary>Rounds an average or an accumulated grade to 2 decimals, midpoint away from zero (R-CAL-04, D-04).</summary>
    /// <param name="value">The unrounded value.</param>
    /// <returns>The value with 2 decimals.</returns>
    public static decimal RoundAverage(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Whether a grade lies on the 0.0 to 5.0 scale (R-CAL-12).</summary>
    /// <param name="grade">The grade to check.</param>
    /// <returns><see langword="true"/> if it is within the scale.</returns>
    public static bool IsOnScale(decimal grade) => grade is >= MinGrade and <= MaxGrade;
}
