namespace TutorFlow.Domain.Attendance;

/// <summary>Loss of a course by absences (R-ASI-01..03, Art. 11).</summary>
public static class AttendancePolicy
{
    /// <summary>Maximum percentage of absences allowed; strictly more than this loses the course (Art. 11).</summary>
    public const int MaxAbsencePercent = 15;

    /// <summary>
    /// Whether the course is lost: <c>absences × 100 &gt; 15 × scheduledClasses</c>, with integers and a strict
    /// comparison (R-ASI-01, CL-02): 4/32 does not lose, 5/32 does.
    /// </summary>
    /// <param name="absences">Registered absences (excused ones are not counted by the API, R-ASI-02).</param>
    /// <param name="scheduledClasses">Scheduled classes of the period; must be positive.</param>
    /// <returns><see langword="true"/> if the limit is exceeded.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Negative absences or non-positive classes.</exception>
    public static bool LosesByAbsences(int absences, int scheduledClasses)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(absences);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scheduledClasses);
        return (long)absences * 100 > (long)MaxAbsencePercent * scheduledClasses;
    }

    /// <summary>Same rule with the "no data" case: <see langword="null"/> absences are unknown, never 0 (R-ASI-03).</summary>
    /// <param name="absences">Registered absences or <see langword="null"/>.</param>
    /// <param name="scheduledClasses">Scheduled classes of the period; must be positive.</param>
    /// <returns>Lost, not lost or unknown.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Negative absences or non-positive classes.</exception>
    public static AbsenceStatus Evaluate(int? absences, int scheduledClasses)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scheduledClasses);
        if (absences is null)
        {
            return AbsenceStatus.Unknown;
        }

        return LosesByAbsences(absences.Value, scheduledClasses) ? AbsenceStatus.Lost : AbsenceStatus.NotLost;
    }
}
