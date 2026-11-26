using TutorFlow.Domain.Attendance;
using TutorFlow.Domain.Grading;

namespace TutorFlow.Domain.Rules;

/// <summary>Habilitation exam rules (R-HAB-01, R-HAB-02, R-HAB-04, Art. 6 and 7).</summary>
public static class HabilitationRules
{
    /// <summary>Lowest final grade that can be habilitated (Art. 6).</summary>
    public const decimal MinimumFinalGrade = 2.0m;

    /// <summary>Highest grade a habilitation can leave as the final grade (R-HAB-04, D-19).</summary>
    public const decimal MaxHabilitationGrade = 3.0m;

    /// <summary>
    /// Whether a course is habilitable: final grade ≥ 2.0 and &lt; 3.0, not lost by absences and not a
    /// non-habilitable type (R-HAB-01, R-HAB-02).
    /// </summary>
    /// <param name="finalGrade">The rounded final grade from the cuts; <see langword="null"/> if the course has none yet.</param>
    /// <param name="absences">Whether the course was lost by absences; <see cref="AbsenceStatus.Unknown"/> if there is no data.</param>
    /// <param name="isNonHabilitableType">Internships and degree projects are never habilitable (R-HAB-02). The API does not
    /// tell the type, so the caller decides.</param>
    /// <returns>Eligible, not eligible or undetermined.</returns>
    public static HabilitationStatus Evaluate(decimal? finalGrade, AbsenceStatus absences, bool isNonHabilitableType = false)
    {
        if (isNonHabilitableType || absences == AbsenceStatus.Lost || finalGrade is null)
        {
            return HabilitationStatus.NotEligible;
        }

        decimal grade = finalGrade.Value;
        if (grade < MinimumFinalGrade || grade >= GradingRules.PassingGrade)
        {
            return HabilitationStatus.NotEligible;
        }

        return absences == AbsenceStatus.Unknown ? HabilitationStatus.Undetermined : HabilitationStatus.Eligible;
    }

    /// <summary>
    /// Final grade after a habilitation exam: the exam grade replaces the final grade and is capped at 3.0
    /// (R-HAB-04, CL-32: 4.5 → 3.0).
    /// </summary>
    /// <param name="examGrade">The grade obtained in the exam.</param>
    /// <returns>The resulting final grade, at most 3.0.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The exam grade is outside 0.0 to 5.0.</exception>
    public static decimal ApplyExamGrade(decimal examGrade)
    {
        if (!GradingRules.IsOnScale(examGrade))
        {
            throw new ArgumentOutOfRangeException(nameof(examGrade), examGrade, "Grade must be between 0.0 and 5.0.");
        }

        return Math.Min(GradingRules.RoundGrade(examGrade), MaxHabilitationGrade);
    }
}
