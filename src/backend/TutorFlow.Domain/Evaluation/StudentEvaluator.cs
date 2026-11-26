using TutorFlow.Domain.Attendance;
using TutorFlow.Domain.Grading;
using TutorFlow.Domain.Rules;

namespace TutorFlow.Domain.Evaluation;

/// <summary>
/// Evaluates a student's period from the already joined rows of the academic API: final grades, losses by absences,
/// habilitation, average, probation and scholarship (R-CAL, R-ASI, R-HAB, R-BEC). Pure and deterministic (R-CAL-09).
/// </summary>
public static class StudentEvaluator
{
    /// <summary>Average required by the performance scholarship (R-BEC-01, Bienestar §3.1).</summary>
    public const decimal ScholarshipMinimumAverage = 4.3m;

    /// <summary>Credits required by the performance scholarship, compared with <c>&gt;=</c> (R-BEC-01).</summary>
    public const int ScholarshipMinimumCredits = 12;

    /// <summary>Average below which the student is on academic probation (R-CAL-08, Art. 20).</summary>
    public const decimal ProbationThreshold = 3.0m;

    /// <summary>Evaluates every course and the student-level conclusions.</summary>
    /// <param name="courses">The courses of the period.</param>
    /// <returns>The evaluation.</returns>
    public static StudentEvaluation Evaluate(IEnumerable<CourseInput> courses)
    {
        ArgumentNullException.ThrowIfNull(courses);
        CourseEvaluation[] evaluated = [.. courses.Select(EvaluateCourse)];

        int enrolledCredits = evaluated.Where(c => c.Credits is > 0).Sum(c => c.Credits!.Value);
        PeriodAverage? average = BuildAverage(evaluated);
        return new StudentEvaluation(
            evaluated,
            average,
            enrolledCredits,
            EvaluateProbation(evaluated),
            EvaluateScholarship(evaluated, enrolledCredits));
    }

    /// <summary>
    /// Evaluates one course. Missing rows, grades off the 0.0 to 5.0 scale, missing or non-positive credits or
    /// classes, and negative absences make the course "inconsistent": it is not used in calculations and nothing is
    /// invented (R-CAL-12, D-20).
    /// </summary>
    /// <param name="course">The course with its rows.</param>
    /// <returns>The course evaluation.</returns>
    public static CourseEvaluation EvaluateCourse(CourseInput course)
    {
        ArgumentNullException.ThrowIfNull(course);
        CourseGradesInput? grades = course.Grades;
        CourseAttendanceInput? attendance = course.Attendance;

        string? reason = FindInconsistency(grades, attendance);
        if (reason is not null)
        {
            return new CourseEvaluation(
                course.Code, course.Name, grades?.Credits, reason, grades?.Cut1, grades?.Cut2, grades?.Cut3,
                GradeFromCuts: null, Accumulated: null, AbsenceStatus.Unknown, CourseOutcome.Inconsistent, HabilitationStatus.Undetermined);
        }

        // FindInconsistency guarantees both rows and their required values exist.
        decimal? finalGrade = grades!.Cut1 is { } c1 && grades.Cut2 is { } c2 && grades.Cut3 is { } c3
            ? GradeCalculator.CalculateFinalGrade(c1, c2, c3)
            : null;
        AccumulatedGrade? accumulated = finalGrade is null
            ? GradeCalculator.CalculateAccumulated(grades.Cut1, grades.Cut2, grades.Cut3)
            : null;
        AbsenceStatus absences = AttendancePolicy.Evaluate(attendance!.Absences, attendance.ScheduledClasses!.Value);

        return new CourseEvaluation(
            course.Code, course.Name, grades.Credits, InconsistencyReason: null, grades.Cut1, grades.Cut2, grades.Cut3,
            finalGrade, accumulated, absences, ClassifyOutcome(finalGrade, absences),
            // R-HAB-02: the course type (internship, degree project) is not in the API and is never deduced from the
            // course name (DUDA-22), so no type is passed and the exclusion by type is not applied here.
            HabilitationRules.Evaluate(finalGrade, absences));
    }

    private static CourseOutcome ClassifyOutcome(decimal? finalGrade, AbsenceStatus absences)
    {
        if (absences == AbsenceStatus.Lost)
        {
            return CourseOutcome.LostByAbsences; // R-CAL-06: whatever the cuts are.
        }

        if (finalGrade is null)
        {
            return CourseOutcome.NoFinalGrade;
        }

        if (finalGrade.Value < GradingRules.PassingGrade)
        {
            return CourseOutcome.Failed;
        }

        return absences == AbsenceStatus.Unknown ? CourseOutcome.PassUndetermined : CourseOutcome.Passed;
    }

    private static string? FindInconsistency(CourseGradesInput? grades, CourseAttendanceInput? attendance)
    {
        if (grades is null)
        {
            return "Missing grades row.";
        }

        if (attendance is null)
        {
            return "Missing attendance row.";
        }

        if (grades.Credits is null or <= 0)
        {
            return "Credits are missing or not positive.";
        }

        foreach (decimal? cut in new[] { grades.Cut1, grades.Cut2, grades.Cut3 })
        {
            if (cut is { } value && !GradingRules.IsOnScale(value))
            {
                return "A cut grade is outside 0.0 to 5.0.";
            }
        }

        if (attendance.ScheduledClasses is null or <= 0)
        {
            return "Scheduled classes are missing or not positive.";
        }

        return attendance.Absences is < 0 ? "Absences are negative." : null;
    }

    private static PeriodAverage? BuildAverage(CourseEvaluation[] courses)
    {
        decimal? baseline = AverageOf(courses, assumeUnknownAbsencesLost: false);
        if (baseline is null)
        {
            return null;
        }

        // R-CAL-10: a course with unknown absences makes the average provisional, with the lost-by-absences alternative.
        string[] unknown = [.. courses.Where(c => c.IsConsistent && c.Absences == AbsenceStatus.Unknown).Select(c => c.Code)];
        string[] inconsistent = [.. courses.Where(c => !c.IsConsistent).Select(c => c.Code)];
        decimal? alternative = unknown.Length > 0 ? AverageOf(courses, assumeUnknownAbsencesLost: true) : null;
        return new PeriodAverage(baseline.Value, unknown.Length > 0 || inconsistent.Length > 0, unknown, alternative, inconsistent);
    }

    private static decimal? AverageOf(CourseEvaluation[] courses, bool assumeUnknownAbsencesLost) =>
        GradeCalculator.CalculateAverage(
            courses.Select(c => (Grade: c.EffectiveGrade(assumeUnknownAbsencesLost), c.Credits))
                .Where(x => x.Grade is not null)
                .Select(x => new WeightedGrade(x.Grade!.Value, x.Credits!.Value)));

    /// <remarks>
    /// Inconsistent courses are left out of the average (R-CAL-12), so with any of them the conclusion is provisional:
    /// callers must report it as such using <see cref="StudentEvaluation.HasInconsistentCourses"/>.
    /// </remarks>
    private static ProbationStatus EvaluateProbation(CourseEvaluation[] courses)
    {
        // Monotone in the unknown absences: comparing both extremes covers every possibility.
        decimal? best = AverageOf(courses, assumeUnknownAbsencesLost: false);
        decimal? worst = AverageOf(courses, assumeUnknownAbsencesLost: true);
        if (best is null || worst is null)
        {
            return ProbationStatus.Undetermined;
        }

        bool bestInProbation = best.Value < ProbationThreshold;
        bool worstInProbation = worst.Value < ProbationThreshold;
        if (bestInProbation == worstInProbation)
        {
            return bestInProbation ? ProbationStatus.InProbation : ProbationStatus.NotInProbation;
        }

        return ProbationStatus.Undetermined;
    }

    private static ScholarshipStatus EvaluateScholarship(CourseEvaluation[] courses, int enrolledCredits)
    {
        // D-05, D-23: any course without final grade (or with unusable data) makes it undetermined, even if no
        // possible result could reach 4.3. A course lost by absences does have a final grade (0.0).
        if (courses.Any(c => c.Outcome is CourseOutcome.NoFinalGrade or CourseOutcome.Inconsistent))
        {
            return ScholarshipStatus.Undetermined;
        }

        bool best = MeetsScholarship(courses, enrolledCredits, assumeUnknownAbsencesLost: false);
        bool worst = MeetsScholarship(courses, enrolledCredits, assumeUnknownAbsencesLost: true);
        if (best == worst)
        {
            return best ? ScholarshipStatus.Eligible : ScholarshipStatus.NotEligible;
        }

        return ScholarshipStatus.Undetermined;
    }

    private static bool MeetsScholarship(CourseEvaluation[] courses, int enrolledCredits, bool assumeUnknownAbsencesLost)
    {
        decimal? average = AverageOf(courses, assumeUnknownAbsencesLost);
        bool anyLost = courses.Any(c => c.IsLost || (assumeUnknownAbsencesLost && c.Absences == AbsenceStatus.Unknown));
        return average is { } value
            && value >= ScholarshipMinimumAverage
            && !anyLost
            && enrolledCredits >= ScholarshipMinimumCredits;
    }
}
