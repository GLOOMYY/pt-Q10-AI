namespace TutorFlow.Domain.Rules;

/// <summary>Why a course is not habilitable (R-HAB-01, R-HAB-02). Only meaningful when the status is <see cref="HabilitationStatus.NotEligible"/>.</summary>
public enum NotHabilitableReason
{
    /// <summary>The course is not "not eligible" (it is eligible or undetermined): there is no reason.</summary>
    None,

    /// <summary>Internship or degree project: never habilitable (R-HAB-02). Only reported when the caller says so (DUDA-22).</summary>
    NonHabilitableType,

    /// <summary>Lost by absences, whatever the cuts (R-HAB-02, R-ASI-01).</summary>
    LostByAbsences,

    /// <summary>The course has no final grade yet (a cut is missing).</summary>
    NoFinalGrade,

    /// <summary>Final grade below 2.0 (R-HAB-01, Art. 6).</summary>
    GradeBelowMinimum,

    /// <summary>Final grade 3.0 or higher: already passed, nothing to habilitate (R-HAB-01).</summary>
    AlreadyPassed,
}
