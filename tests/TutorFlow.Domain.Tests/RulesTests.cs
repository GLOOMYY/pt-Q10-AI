using TutorFlow.Domain.Attendance;
using TutorFlow.Domain.Grading;
using TutorFlow.Domain.Rules;

namespace TutorFlow.Domain.Tests;

/// <summary>Unit tests of the habilitation, cancellation and grading edge rules that no ESC-RAR/ESC-SIM unit scenario covers.</summary>
public class RulesTests
{
    [Theory]
    [InlineData("2.0", AbsenceStatus.NotLost, HabilitationStatus.Eligible)]
    [InlineData("2.6", AbsenceStatus.NotLost, HabilitationStatus.Eligible)] // EST-002 Cálculo
    [InlineData("2.3", AbsenceStatus.NotLost, HabilitationStatus.Eligible)] // EST-005 Estadística
    [InlineData("1.8", AbsenceStatus.NotLost, HabilitationStatus.NotEligible)] // CL-04: below 2.0
    [InlineData("3.0", AbsenceStatus.NotLost, HabilitationStatus.NotEligible)] // already passed
    [InlineData("2.6", AbsenceStatus.Lost, HabilitationStatus.NotEligible)] // R-HAB-02
    [InlineData("2.6", AbsenceStatus.Unknown, HabilitationStatus.Undetermined)] // R-ASI-03
    public void Habilitation_follows_R_HAB_01_and_R_HAB_02(string grade, AbsenceStatus absences, HabilitationStatus expected) =>
        Assert.Equal(expected, HabilitationRules.Evaluate(decimal.Parse(grade, System.Globalization.CultureInfo.InvariantCulture), absences));

    [Fact]
    public void Habilitation_is_not_possible_without_final_grade_or_for_excluded_types()
    {
        Assert.Equal(HabilitationStatus.NotEligible, HabilitationRules.Evaluate(null, AbsenceStatus.NotLost));
        Assert.Equal(HabilitationStatus.NotEligible, HabilitationRules.Evaluate(2.5m, AbsenceStatus.NotLost, isNonHabilitableType: true));
    }

    [Fact]
    public void Not_habilitable_reason_follows_the_precedence_of_Evaluate_DUDA_42()
    {
        Assert.Equal(NotHabilitableReason.NonHabilitableType, HabilitationRules.GetNotHabilitableReason(2.5m, AbsenceStatus.Lost, isNonHabilitableType: true));
        Assert.Equal(NotHabilitableReason.LostByAbsences, HabilitationRules.GetNotHabilitableReason(2.6m, AbsenceStatus.Lost));
        Assert.Equal(NotHabilitableReason.NoFinalGrade, HabilitationRules.GetNotHabilitableReason(null, AbsenceStatus.NotLost));
        Assert.Equal(NotHabilitableReason.GradeBelowMinimum, HabilitationRules.GetNotHabilitableReason(1.8m, AbsenceStatus.NotLost));
        Assert.Equal(NotHabilitableReason.AlreadyPassed, HabilitationRules.GetNotHabilitableReason(3.0m, AbsenceStatus.NotLost));
        Assert.Equal(NotHabilitableReason.None, HabilitationRules.GetNotHabilitableReason(2.0m, AbsenceStatus.NotLost));
        Assert.Equal(NotHabilitableReason.None, HabilitationRules.GetNotHabilitableReason(2.6m, AbsenceStatus.Unknown));
    }

    [Theory]
    [InlineData("4.5", "3.0")] // CL-32 / R-HAB-04 / D-19
    [InlineData("3.0", "3.0")]
    [InlineData("2.4", "2.4")]
    public void Habilitation_exam_grade_is_capped_at_3_0(string exam, string expected) =>
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            HabilitationRules.ApplyExamGrade(decimal.Parse(exam, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Cancellation_keeps_at_least_8_credits_CL_15()
    {
        Assert.True(CancellationRules.KeepsMinimumCredits(12, [4]));
        Assert.False(CancellationRules.KeepsMinimumCredits(12, [3, 3]));
    }

    [Fact]
    public void Exactly_3_0_passes_and_midpoint_rounds_away_from_zero_R_CAL_07_R_CAL_03()
    {
        Assert.True(GradeCalculator.CalculateFinalGrade(3.0m, 3.0m, 3.0m) >= GradingRules.PassingGrade);
        Assert.Equal(4.5m, GradingRules.RoundGrade(4.45m));
        Assert.Equal(3.5m, GradingRules.RoundGrade(3.45m));
        Assert.Equal(4.1m, GradingRules.RoundGrade(4.05m));
        Assert.Equal(2.28m, GradingRules.RoundAverage(2.275m));
    }

    [Fact]
    public void Accumulated_grade_has_2_decimals_and_the_evaluated_percent_D_04()
    {
        AccumulatedGrade? accumulated = GradeCalculator.CalculateAccumulated(3.9m, 4.1m, null); // EST-006 Estadística
        Assert.Equal(new AccumulatedGrade(2.40m, 60), accumulated);
        Assert.Null(GradeCalculator.CalculateAccumulated(null, null, null));
    }

    [Fact]
    public void Grades_outside_the_scale_are_never_computed_R_CAL_12()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GradeCalculator.CalculateFinalGrade(5.1m, 3m, 3m));
        Assert.Throws<ArgumentOutOfRangeException>(() => GradeCalculator.CalculateFinalGrade(-0.1m, 3m, 3m));
    }
}
