namespace TutorFlow.Domain.Grading;

/// <summary>Simulation of the grade needed in the pending cuts (R-CAL-11, D-15, CL-28).</summary>
public static class RequiredGradeSimulator
{
    /// <summary>
    /// Finds the lowest grade <c>x</c> (1 decimal, the same in every pending cut) such that
    /// <c>accumulated + pendingWeight · x ≥ target − 0.05</c>, so the <b>rounded</b> final grade reaches the target.
    /// Example: 2.40 + 0.4·x ≥ 2.95 → x ≥ 1.375 → 1.4 (not 1.5).
    /// </summary>
    /// <param name="cut1">Cut 1 grade or <see langword="null"/> if pending.</param>
    /// <param name="cut2">Cut 2 grade or <see langword="null"/> if pending.</param>
    /// <param name="cut3">Cut 3 grade or <see langword="null"/> if pending.</param>
    /// <param name="target">The desired rounded final grade (3.0 to pass, 4.3 for the scholarship).</param>
    /// <returns>The minimum grade, or a special outcome (unreachable, secured, no pending cuts).</returns>
    /// <exception cref="ArgumentOutOfRangeException">A grade or the target is outside 0.0 to 5.0.</exception>
    public static RequiredGradeResult Calculate(decimal? cut1, decimal? cut2, decimal? cut3, decimal target)
    {
        if (!GradingRules.IsOnScale(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "Target must be between 0.0 and 5.0.");
        }

        (decimal accumulated, _, decimal pendingWeight) = GradeCalculator.SumGradedCuts(cut1, cut2, cut3);
        if (pendingWeight == 0m)
        {
            return new RequiredGradeResult(SimulationOutcome.NoPendingCuts, null);
        }

        // Exact decimal arithmetic (D-06): with double, (2.95 - 1.20) / 0.7 = 2.5000000000000004 → 2.6 (wrong).
        decimal exact = (target - GradingRules.HalfRoundingStep - accumulated) / pendingWeight;
        decimal required = Math.Ceiling(exact * 10m) / 10m;

        if (required <= GradingRules.MinGrade)
        {
            return new RequiredGradeResult(SimulationOutcome.Secured, null);
        }

        return required > GradingRules.MaxGrade
            ? new RequiredGradeResult(SimulationOutcome.Unreachable, null)
            : new RequiredGradeResult(SimulationOutcome.Required, required);
    }
}
