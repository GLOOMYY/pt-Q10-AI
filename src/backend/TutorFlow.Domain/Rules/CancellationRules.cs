namespace TutorFlow.Domain.Rules;

/// <summary>
/// Credit rule of course cancellation (R-HAB-03, Art. 16, CL-15). The week-8 deadline (Art. 15) is not computable:
/// the API does not expose the current week, so it is never asserted.
/// </summary>
public static class CancellationRules
{
    /// <summary>Minimum credits that must remain enrolled after cancelling (Art. 16).</summary>
    public const int MinimumRemainingCredits = 8;

    /// <summary>Last week in which a course can be cancelled (Art. 15). Informational: never compared with a date.</summary>
    public const int LastCancellationWeek = 8;

    /// <summary>
    /// Whether cancelling the given courses keeps at least 8 credits: with 12 credits, one course of 4 can be
    /// cancelled (8 remain) but not two of 3 (6 remain) (CL-15).
    /// </summary>
    /// <param name="enrolledCredits">Credits currently enrolled.</param>
    /// <param name="creditsToCancel">Credits of each course to cancel.</param>
    /// <returns><see langword="true"/> if at least 8 credits remain.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Negative credits.</exception>
    public static bool KeepsMinimumCredits(int enrolledCredits, IEnumerable<int> creditsToCancel)
    {
        ArgumentNullException.ThrowIfNull(creditsToCancel);
        ArgumentOutOfRangeException.ThrowIfNegative(enrolledCredits);
        int remaining = enrolledCredits;
        foreach (int credits in creditsToCancel)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(credits, nameof(creditsToCancel));
            remaining -= credits;
        }

        return remaining >= MinimumRemainingCredits;
    }
}
