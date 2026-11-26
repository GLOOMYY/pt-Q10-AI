namespace TutorFlow.Domain.Evaluation;

/// <summary>Academic standing of a course in the period (R-CAL-05..07, R-CAL-12).</summary>
public enum CourseOutcome
{
    /// <summary>The data is inconsistent and is not used in calculations (R-CAL-12).</summary>
    Inconsistent,

    /// <summary>A cut is missing: no final grade; neither passed nor lost (R-CAL-05).</summary>
    NoFinalGrade,

    /// <summary>Lost by absences: final grade 0.0 (R-CAL-06).</summary>
    LostByAbsences,

    /// <summary>Final grade below 3.0 (R-CAL-07).</summary>
    Failed,

    /// <summary>Final grade of 3.0 or more and not lost by absences.</summary>
    Passed,

    /// <summary>Final grade of 3.0 or more but the absences are unknown, so it may still be lost (R-ASI-03).</summary>
    PassUndetermined,
}
