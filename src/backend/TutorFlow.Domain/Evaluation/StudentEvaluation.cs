namespace TutorFlow.Domain.Evaluation;

/// <summary>Academic probation of the period (R-CAL-08, Art. 20).</summary>
public enum ProbationStatus
{
    /// <summary>The period average is below 3.0.</summary>
    InProbation,

    /// <summary>The period average is 3.0 or more, whatever the unknown data turns out to be.</summary>
    NotInProbation,

    /// <summary>There is no average, or the answer depends on unknown absences (R-CAL-10, R-CAL-12).</summary>
    Undetermined,
}

/// <summary>Eligibility for the performance scholarship (R-BEC-01, R-BEC-02, D-05, D-23).</summary>
public enum ScholarshipStatus
{
    /// <summary>Average of 4.3 or more, no lost course and 12 credits or more.</summary>
    Eligible,

    /// <summary>At least one requirement fails with the data at hand.</summary>
    NotEligible,

    /// <summary>"No determinable todavía": a course has no final grade, data is inconsistent, or the answer depends on unknown absences (D-05, D-23).</summary>
    Undetermined,
}

/// <summary>
/// Weighted average of the period (R-CAL-04) with its provisional flags (R-CAL-10).
/// </summary>
/// <param name="Value">Average with 2 decimals, using unknown absences as "not lost".</param>
/// <param name="IsProvisional">True if it depends on unknown absences or excludes inconsistent courses.</param>
/// <param name="UnknownAbsenceCourseCodes">Codes of the courses whose unknown absences make the average provisional.</param>
/// <param name="ValueIfUnknownAbsencesLost">The average if every course in <paramref name="UnknownAbsenceCourseCodes"/> had been lost by absences (0.0), or <see langword="null"/> if there are none (EST-003: 4.11 → 3.34).</param>
/// <param name="InconsistentCourseCodes">Codes of the courses left out because their data is inconsistent (R-CAL-12).</param>
public sealed record PeriodAverage(
    decimal Value,
    bool IsProvisional,
    IReadOnlyList<string> UnknownAbsenceCourseCodes,
    decimal? ValueIfUnknownAbsencesLost,
    IReadOnlyList<string> InconsistentCourseCodes);

/// <summary>Everything the use case needs to answer about one student (R-CAL-09: computed by the backend, not the LLM).</summary>
/// <param name="Courses">Evaluation of each course, in input order.</param>
/// <param name="Average">The period average, or <see langword="null"/> for "sin promedio en el periodo" (R-CAL-12, D-20).</param>
/// <param name="EnrolledCredits">Credits of the enrolled courses, with or without final grade (D-05).</param>
/// <param name="Probation">Academic probation (R-CAL-08). The loss of the place (Art. 21) is never determinable: only one period exists.</param>
/// <param name="Scholarship">Performance scholarship eligibility (R-BEC-01..02).</param>
public sealed record StudentEvaluation(
    IReadOnlyList<CourseEvaluation> Courses,
    PeriodAverage? Average,
    int EnrolledCredits,
    ProbationStatus Probation,
    ScholarshipStatus Scholarship)
{
    /// <summary>Whether some course was left out for inconsistent data, so every conclusion is provisional (R-CAL-12).</summary>
    public bool HasInconsistentCourses => Courses.Any(c => !c.IsConsistent);

    /// <summary>Courses that count as lost (failed or lost by absences).</summary>
    public IEnumerable<CourseEvaluation> LostCourses => Courses.Where(c => c.IsLost);

    /// <summary>Courses eligible for a habilitation exam (R-HAB-01).</summary>
    public IEnumerable<CourseEvaluation> HabilitableCourses =>
        Courses.Where(c => c.Habilitation == Rules.HabilitationStatus.Eligible);
}
