namespace TutorFlow.Domain.Grading;

/// <summary>Pure grade calculations (R-CAL-01..05, R-CAL-12). Deterministic, no I/O (R-CAL-09).</summary>
public static class GradeCalculator
{
    /// <summary>
    /// Final grade = <c>0.3·c1 + 0.3·c2 + 0.4·c3</c>, rounded to 1 decimal with midpoint away from zero
    /// (R-CAL-01..03, CL-07, CL-17: 4.45 → 4.5).
    /// </summary>
    /// <param name="cut1">Cut 1 grade.</param>
    /// <param name="cut2">Cut 2 grade.</param>
    /// <param name="cut3">Cut 3 grade.</param>
    /// <returns>The final grade with 1 decimal.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A grade is outside 0.0 to 5.0: invalid data is never computed (R-CAL-12).</exception>
    public static decimal CalculateFinalGrade(decimal cut1, decimal cut2, decimal cut3)
    {
        EnsureOnScale(cut1, nameof(cut1));
        EnsureOnScale(cut2, nameof(cut2));
        EnsureOnScale(cut3, nameof(cut3));
        return GradingRules.RoundGrade(
            (GradingRules.Cut1Weight * cut1) + (GradingRules.Cut2Weight * cut2) + (GradingRules.Cut3Weight * cut3));
    }

    /// <summary>
    /// Accumulated grade of the graded cuts (R-CAL-05, D-04): <c>Σ(weight × cut)</c> with 2 decimals, plus the
    /// evaluated percentage. A <see langword="null"/> cut is "not graded yet", never 0 (API-10).
    /// </summary>
    /// <param name="cut1">Cut 1 grade or <see langword="null"/>.</param>
    /// <param name="cut2">Cut 2 grade or <see langword="null"/>.</param>
    /// <param name="cut3">Cut 3 grade or <see langword="null"/>.</param>
    /// <returns>The accumulated grade, or <see langword="null"/> if no cut is graded.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A grade is outside 0.0 to 5.0 (R-CAL-12).</exception>
    public static AccumulatedGrade? CalculateAccumulated(decimal? cut1, decimal? cut2, decimal? cut3)
    {
        (decimal sum, decimal gradedWeight, _) = SumGradedCuts(cut1, cut2, cut3);
        if (gradedWeight == 0m)
        {
            return null;
        }

        return new AccumulatedGrade(GradingRules.RoundAverage(sum), (int)(gradedWeight * 100m));
    }

    /// <summary>
    /// Weighted average of the period = <c>Σ(final × credits) / Σ credits</c> over the already rounded finals,
    /// with 2 decimals and midpoint away from zero (R-CAL-04: 2.275 → 2.28).
    /// </summary>
    /// <param name="grades">Final grades with their credits; courses lost by absences come with 0.0 (R-CAL-06).</param>
    /// <returns>The average, or <see langword="null"/> when there is nothing to average ("sin promedio", R-CAL-12, D-20):
    /// it never divides by 0 credits.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A grade is off the scale or the credits are not positive.</exception>
    public static decimal? CalculateAverage(IEnumerable<WeightedGrade> grades)
    {
        ArgumentNullException.ThrowIfNull(grades);
        decimal weighted = 0m;
        int credits = 0;
        foreach (WeightedGrade item in grades)
        {
            EnsureOnScale(item.Grade, nameof(grades));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(item.Credits, nameof(grades));
            weighted += item.Grade * item.Credits;
            credits += item.Credits;
        }

        return credits == 0 ? null : GradingRules.RoundAverage(weighted / credits);
    }

    /// <summary>
    /// Sums <c>weight × cut</c> of the graded cuts without rounding, with the weight of the graded and of the
    /// pending ones. Shared with the simulator so both use exactly the same arithmetic (R-CAL-11).
    /// </summary>
    internal static (decimal Sum, decimal GradedWeight, decimal PendingWeight) SumGradedCuts(
        decimal? cut1, decimal? cut2, decimal? cut3)
    {
        decimal sum = 0m;
        decimal graded = 0m;
        decimal pending = 0m;
        Accumulate(cut1, GradingRules.Cut1Weight, nameof(cut1));
        Accumulate(cut2, GradingRules.Cut2Weight, nameof(cut2));
        Accumulate(cut3, GradingRules.Cut3Weight, nameof(cut3));
        return (sum, graded, pending);

        void Accumulate(decimal? cut, decimal weight, string name)
        {
            if (cut is null)
            {
                pending += weight;
                return;
            }

            EnsureOnScale(cut.Value, name);
            sum += weight * cut.Value;
            graded += weight;
        }
    }

    private static void EnsureOnScale(decimal grade, string name)
    {
        if (!GradingRules.IsOnScale(grade))
        {
            throw new ArgumentOutOfRangeException(name, grade, "Grade must be between 0.0 and 5.0.");
        }
    }
}
