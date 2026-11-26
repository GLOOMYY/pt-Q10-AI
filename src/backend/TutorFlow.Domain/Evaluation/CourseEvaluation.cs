using TutorFlow.Domain.Attendance;
using TutorFlow.Domain.Grading;
using TutorFlow.Domain.Rules;

namespace TutorFlow.Domain.Evaluation;

/// <summary>Result of evaluating one course (R-CAL-05..07, R-CAL-12, R-ASI, R-HAB).</summary>
/// <param name="Code">Course code.</param>
/// <param name="Name">Course name.</param>
/// <param name="Credits">Credits, or <see langword="null"/> if unknown.</param>
/// <param name="InconsistencyReason">Why the course is inconsistent (English, for logs), or <see langword="null"/> if it is consistent.</param>
/// <param name="Cut1">Cut 1 as received.</param>
/// <param name="Cut2">Cut 2 as received.</param>
/// <param name="Cut3">Cut 3 as received.</param>
/// <param name="GradeFromCuts">Rounded grade computed from the three cuts (1 decimal), or <see langword="null"/> if a cut is missing or the course is inconsistent. It is NOT the final grade of a course lost by absences (R-CAL-06): use <see cref="FinalGrade"/> for that.</param>
/// <param name="Accumulated">Accumulated grade when the course has no final grade (D-04), otherwise <see langword="null"/>.</param>
/// <param name="Absences">Loss by absences.</param>
/// <param name="Outcome">Academic standing.</param>
/// <param name="Habilitation">Habilitation eligibility.</param>
public sealed record CourseEvaluation(
    string Code,
    string Name,
    int? Credits,
    string? InconsistencyReason,
    decimal? Cut1,
    decimal? Cut2,
    decimal? Cut3,
    decimal? GradeFromCuts,
    AccumulatedGrade? Accumulated,
    AbsenceStatus Absences,
    CourseOutcome Outcome,
    HabilitationStatus Habilitation)
{
    /// <summary>
    /// The final grade (definitiva): 0.0 when the course was lost by absences, whatever the cuts (R-CAL-06, CL-03,
    /// Art. 11); the grade from the cuts otherwise; <see langword="null"/> if the course has none (a cut is missing,
    /// R-CAL-05) or is inconsistent (R-CAL-12). With unknown absences it is the grade from the cuts, which is provisional.
    /// </summary>
    public decimal? FinalGrade =>
        !IsConsistent ? null : Absences == AbsenceStatus.Lost ? 0.0m : GradeFromCuts;

    /// <summary>Whether the data is usable (R-CAL-12).</summary>
    public bool IsConsistent => InconsistencyReason is null;

    /// <summary>Whether the course counts as lost: failed or lost by absences (glossary "Asignatura perdida").</summary>
    public bool IsLost => Outcome is CourseOutcome.Failed or CourseOutcome.LostByAbsences;

    /// <summary>
    /// The grade used in the average: 0.0 when lost by absences, whatever the cuts (R-CAL-06); the final grade
    /// otherwise; <see langword="null"/> when the course has none or is inconsistent (R-CAL-05, R-CAL-12).
    /// </summary>
    /// <param name="assumeUnknownAbsencesLost">Treat unknown absences as lost: the worst case of R-CAL-10.</param>
    /// <returns>The grade to average, or <see langword="null"/> if the course does not enter the average.</returns>
    public decimal? EffectiveGrade(bool assumeUnknownAbsencesLost = false)
    {
        if (!IsConsistent)
        {
            return null;
        }

        bool lost = Absences == AbsenceStatus.Lost || (assumeUnknownAbsencesLost && Absences == AbsenceStatus.Unknown);
        return lost ? 0.0m : GradeFromCuts;
    }

    /// <summary>
    /// Simulates the grade needed in the pending cuts (R-CAL-11, CL-29). A course lost by absences cannot be saved by
    /// any grade; a course with all cuts graded has nothing to simulate.
    /// </summary>
    /// <param name="target">Desired rounded final grade; 3.0 by default (pass).</param>
    /// <returns>The simulation. With unknown absences the result is flagged provisional in <see cref="CourseSimulation.IsProvisional"/>.</returns>
    public CourseSimulation Simulate(decimal target = GradingRules.PassingGrade)
    {
        if (!IsConsistent)
        {
            return new CourseSimulation(new RequiredGradeResult(SimulationOutcome.Inconsistent, null), IsProvisional: true);
        }

        if (Absences == AbsenceStatus.Lost)
        {
            return new CourseSimulation(new RequiredGradeResult(SimulationOutcome.LostByAbsences, null), IsProvisional: false);
        }

        return new CourseSimulation(
            RequiredGradeSimulator.Calculate(Cut1, Cut2, Cut3, target),
            IsProvisional: Absences == AbsenceStatus.Unknown);
    }
}

/// <summary>A course simulation (R-CAL-11). Always a projection, never an official grade.</summary>
/// <param name="Result">The simulation result.</param>
/// <param name="IsProvisional">The result depends on data that is missing (unknown absences, inconsistent data).</param>
public sealed record CourseSimulation(RequiredGradeResult Result, bool IsProvisional);
