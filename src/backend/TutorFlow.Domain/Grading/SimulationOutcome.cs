namespace TutorFlow.Domain.Grading;

/// <summary>Kind of result of a "what do I need?" simulation (R-CAL-11, CL-29).</summary>
public enum SimulationOutcome
{
    /// <summary>A minimum grade <c>x</c> was found; see <see cref="RequiredGradeResult.Grade"/>.</summary>
    Required,

    /// <summary>The goal is unreachable: <c>x</c> would exceed 5.0.</summary>
    Unreachable,

    /// <summary>The goal is already secured with any grade: <c>x</c> is at most 0.0.</summary>
    Secured,

    /// <summary>All three cuts are graded: there is nothing to simulate.</summary>
    NoPendingCuts,

    /// <summary>The course was lost by absences: no grade saves it (R-CAL-11, Art. 11). Only produced at course level.</summary>
    LostByAbsences,

    /// <summary>The course data is inconsistent and is not used for calculations (R-CAL-12). Only produced at course level.</summary>
    Inconsistent,
}
