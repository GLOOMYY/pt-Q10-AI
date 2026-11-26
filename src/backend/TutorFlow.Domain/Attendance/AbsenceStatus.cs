namespace TutorFlow.Domain.Attendance;

/// <summary>Whether a course was lost by absences (R-ASI-01, R-ASI-03).</summary>
public enum AbsenceStatus
{
    /// <summary>More than 15 % of the scheduled classes were missed.</summary>
    Lost,

    /// <summary>The limit was not exceeded.</summary>
    NotLost,

    /// <summary>Absences are <see langword="null"/>: it cannot be determined; never assumed to be 0 (R-ASI-03, API-10).</summary>
    Unknown,
}
