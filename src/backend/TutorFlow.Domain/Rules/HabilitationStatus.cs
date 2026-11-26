namespace TutorFlow.Domain.Rules;

/// <summary>Whether a course can be taken to a habilitation exam (R-HAB-01, R-HAB-02).</summary>
public enum HabilitationStatus
{
    /// <summary>Final grade from 2.0 up to (not including) 3.0 and not lost by absences.</summary>
    Eligible,

    /// <summary>Not habilitable: grade below 2.0, already passed, no final grade, lost by absences or excluded type.</summary>
    NotEligible,

    /// <summary>It depends on data that is missing (unknown absences or inconsistent data), so it cannot be said (R-ASI-03, R-CAL-12).</summary>
    Undetermined,
}
