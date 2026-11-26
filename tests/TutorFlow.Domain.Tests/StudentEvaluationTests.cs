using System.Text.Json;
using TutorFlow.Domain.Attendance;
using TutorFlow.Domain.Evaluation;
using TutorFlow.Domain.Grading;
using TutorFlow.Domain.Rules;
using TutorFlow.Domain.Tests.Scenarios;

namespace TutorFlow.Domain.Tests;

/// <summary>
/// Checks the student evaluation against the reference values of SPEC §6.10, building the courses from the real
/// <c>material/mock-api/datos.json</c> (no data is copied into the tests).
/// </summary>
public class StudentEvaluationTests
{
    private static readonly Lazy<JsonDocument> Data = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(ScenarioCatalog.RepositoryRoot, "material", "mock-api", "datos.json"))));

    private static decimal? Dec(JsonElement e, string name) =>
        e.GetProperty(name).ValueKind == JsonValueKind.Null ? null : e.GetProperty(name).GetDecimal();

    private static int? Int(JsonElement e, string name) =>
        e.GetProperty(name).ValueKind == JsonValueKind.Null ? null : e.GetProperty(name).GetInt32();

    private static List<CourseInput> CoursesOf(string studentId)
    {
        JsonElement root = Data.Value.RootElement;
        JsonElement[] attendance = [.. root.GetProperty("asistencia").EnumerateArray().Where(a => a.GetProperty("estudianteId").GetString() == studentId)];
        return [.. root.GetProperty("notas").EnumerateArray()
            .Where(n => n.GetProperty("estudianteId").GetString() == studentId)
            .Select(n =>
            {
                string code = n.GetProperty("codigo").GetString()!;
                JsonElement? a = attendance.Cast<JsonElement?>().FirstOrDefault(x => x!.Value.GetProperty("codigo").GetString() == code);
                return new CourseInput(
                    code,
                    n.GetProperty("asignatura").GetString()!,
                    new CourseGradesInput(Int(n, "creditos"), Dec(n, "corte1"), Dec(n, "corte2"), Dec(n, "corte3")),
                    a is null ? null : new CourseAttendanceInput(Int(a.Value, "clasesProgramadas"), Int(a.Value, "inasistencias")));
            })];
    }

    private static StudentEvaluation EvaluateStudent(string id) => StudentEvaluator.Evaluate(CoursesOf(id));

    private static decimal?[] Effective(StudentEvaluation e) => [.. e.Courses.Select(c => c.EffectiveGrade())];

    [Fact]
    public void EST_001_average_4_39_and_scholarship_eligible()
    {
        StudentEvaluation e = EvaluateStudent("EST-001");
        Assert.Equal([4.5m, 4.8m, 4.1m, 4.0m], Effective(e));
        Assert.Equal(4.39m, e.Average!.Value);
        Assert.False(e.Average.IsProvisional);
        Assert.Equal(12, e.EnrolledCredits);
        Assert.Equal(ProbationStatus.NotInProbation, e.Probation);
        Assert.Equal(ScholarshipStatus.Eligible, e.Scholarship);
    }

    [Fact]
    public void EST_002_lost_by_absences_counts_as_0_0_and_is_in_probation()
    {
        StudentEvaluation e = EvaluateStudent("EST-002");
        Assert.Equal([2.6m, 3.5m, 0.0m, 3.2m], Effective(e));
        Assert.Equal(2.28m, e.Average!.Value);
        CourseEvaluation databases = e.Courses.Single(c => c.Code == "BD-203");
        Assert.Equal(CourseOutcome.LostByAbsences, databases.Outcome);
        Assert.Equal(0.0m, databases.FinalGrade); // R-CAL-06, CL-03: the final grade is 0.0
        Assert.Equal(3.8m, databases.GradeFromCuts); // the cuts alone give 3.8, which is not the final grade
        Assert.Equal(HabilitationStatus.NotEligible, databases.Habilitation);
        Assert.Equal(["CAL-201"], e.HabilitableCourses.Select(c => c.Code));
        Assert.Equal(ProbationStatus.InProbation, e.Probation);
        Assert.Equal(ScholarshipStatus.NotEligible, e.Scholarship);
    }

    [Fact]
    public void EST_003_average_is_provisional_with_the_alternative_if_English_was_lost_R_CAL_10_CL_26()
    {
        StudentEvaluation e = EvaluateStudent("EST-003");
        Assert.Equal(4.11m, e.Average!.Value);
        Assert.True(e.Average.IsProvisional);
        Assert.Equal(["ING-204"], e.Average.UnknownAbsenceCourseCodes);
        Assert.Equal(3.34m, e.Average.ValueIfUnknownAbsencesLost);
        CourseEvaluation english = e.Courses.Single(c => c.Code == "ING-204");
        Assert.Equal(4.6m, english.FinalGrade);
        Assert.Equal(AbsenceStatus.Unknown, english.Absences);
        Assert.Equal(CourseOutcome.PassUndetermined, english.Outcome);
        Assert.Equal(ProbationStatus.NotInProbation, e.Probation); // in neither case
        Assert.Equal(ScholarshipStatus.NotEligible, e.Scholarship); // 4.11 < 4.3 in both cases
    }

    [Fact]
    public void EST_004_average_3_88()
    {
        StudentEvaluation e = EvaluateStudent("EST-004");
        Assert.Equal([3.9m, 3.3m, 4.0m, 4.5m], Effective(e));
        Assert.Equal(3.88m, e.Average!.Value);
        Assert.Equal(ScholarshipStatus.NotEligible, e.Scholarship);
    }

    [Fact]
    public void EST_005_is_in_probation_and_only_Statistics_is_habilitable_CL_04()
    {
        StudentEvaluation e = EvaluateStudent("EST-005");
        Assert.Equal([3.2m, 1.8m, 2.3m, 3.8m], Effective(e));
        Assert.Equal(2.65m, e.Average!.Value);
        Assert.Equal(["EST-103"], e.HabilitableCourses.Select(c => c.Code));
        Assert.Equal(["MIC-102", "EST-103"], e.LostCourses.Select(c => c.Code));
        Assert.Equal(ProbationStatus.InProbation, e.Probation);
    }

    [Fact]
    public void EST_006_course_without_final_grade_stays_out_of_the_average_and_the_scholarship_is_undetermined_CL_05_D_23()
    {
        StudentEvaluation e = EvaluateStudent("EST-006");
        CourseEvaluation statistics = e.Courses.Single(c => c.Code == "EST-103");
        Assert.Equal(CourseOutcome.NoFinalGrade, statistics.Outcome);
        Assert.Null(statistics.FinalGrade);
        Assert.Null(statistics.EffectiveGrade());
        Assert.Equal(new AccumulatedGrade(2.40m, 60), statistics.Accumulated);
        Assert.False(statistics.IsLost);
        Assert.Equal(4.05m, e.Average!.Value); // over 8 credits
        Assert.Equal(12, e.EnrolledCredits); // credits count with or without final grade (D-05)
        Assert.Equal(ScholarshipStatus.Undetermined, e.Scholarship);
        Assert.Equal(ProbationStatus.NotInProbation, e.Probation);
    }

    [Fact]
    public void EST_006_simulation_needs_1_4_not_1_5_CL_28()
    {
        CourseSimulation simulation = EvaluateStudent("EST-006").Courses.Single(c => c.Code == "EST-103").Simulate();
        Assert.Equal(new RequiredGradeResult(SimulationOutcome.Required, 1.4m), simulation.Result);
        Assert.False(simulation.IsProvisional);
    }

    [Fact]
    public void Simulation_of_a_course_lost_by_absences_or_already_graded_has_nothing_to_compute_CL_29()
    {
        StudentEvaluation e = EvaluateStudent("EST-002");
        Assert.Equal(SimulationOutcome.LostByAbsences, e.Courses.Single(c => c.Code == "BD-203").Simulate().Result.Outcome);
        Assert.Equal(SimulationOutcome.NoPendingCuts, e.Courses.Single(c => c.Code == "CAL-201").Simulate().Result.Outcome);
    }

    [Fact]
    public void Student_without_courses_or_without_final_grades_has_no_average_R_CAL_12()
    {
        StudentEvaluation none = StudentEvaluator.Evaluate([]);
        Assert.Null(none.Average);
        Assert.Equal(ProbationStatus.Undetermined, none.Probation);
        Assert.Equal(0, none.EnrolledCredits);

        StudentEvaluation noFinals = StudentEvaluator.Evaluate(
        [
            new CourseInput("X-1", "X", new CourseGradesInput(3, 4.0m, null, null), new CourseAttendanceInput(32, 0)),
        ]);
        Assert.Null(noFinals.Average);
        Assert.Equal(ScholarshipStatus.Undetermined, noFinals.Scholarship);
    }

    [Fact]
    public void Missing_attendance_row_or_grade_off_the_scale_is_inconsistent_and_never_computed_R_CAL_12()
    {
        var good = new CourseInput("G-1", "Good", new CourseGradesInput(4, 4.0m, 4.0m, 4.0m), new CourseAttendanceInput(32, 0));
        var noAttendance = new CourseInput("A-1", "NoAttendance", new CourseGradesInput(3, 4.0m, 4.0m, 4.0m), null);
        var noGrades = new CourseInput("B-1", "NoGrades", null, new CourseAttendanceInput(32, 0));
        var offScale = new CourseInput("C-1", "OffScale", new CourseGradesInput(3, 5.5m, 4.0m, 4.0m), new CourseAttendanceInput(32, 0));

        StudentEvaluation e = StudentEvaluator.Evaluate([good, noAttendance, noGrades, offScale]);

        Assert.All(e.Courses.Skip(1), c => Assert.Equal(CourseOutcome.Inconsistent, c.Outcome));
        Assert.All(e.Courses.Skip(1), c => Assert.Null(c.FinalGrade));
        Assert.Equal(4.0m, e.Average!.Value); // only the consistent course
        Assert.True(e.Average.IsProvisional);
        Assert.Equal(["A-1", "B-1", "C-1"], e.Average.InconsistentCourseCodes);
        Assert.True(e.HasInconsistentCourses);
        Assert.Equal(ScholarshipStatus.Undetermined, e.Scholarship);
        Assert.Equal(SimulationOutcome.Inconsistent, e.Courses[3].Simulate().Result.Outcome);
    }

    [Fact]
    public void Unknown_absences_make_the_scholarship_undetermined_when_the_outcome_would_change_R_CAL_10()
    {
        // Average 4.5 with 12 credits: eligible unless the course with unknown absences was lost.
        StudentEvaluation e = StudentEvaluator.Evaluate(
        [
            new CourseInput("A-1", "A", new CourseGradesInput(6, 4.5m, 4.5m, 4.5m), new CourseAttendanceInput(32, 0)),
            new CourseInput("B-1", "B", new CourseGradesInput(6, 4.5m, 4.5m, 4.5m), new CourseAttendanceInput(32, null)),
        ]);
        Assert.Equal(4.5m, e.Average!.Value);
        Assert.Equal(2.25m, e.Average.ValueIfUnknownAbsencesLost);
        Assert.Equal(ScholarshipStatus.Undetermined, e.Scholarship);
    }

    [Fact]
    public void Scholarship_requires_12_credits_with_a_greater_or_equal_comparison_R_BEC_01()
    {
        CourseInput Course(int credits) =>
            new("A-1", "A", new CourseGradesInput(credits, 4.5m, 4.5m, 4.5m), new CourseAttendanceInput(32, 0));

        Assert.Equal(ScholarshipStatus.Eligible, StudentEvaluator.Evaluate([Course(12)]).Scholarship);
        Assert.Equal(ScholarshipStatus.NotEligible, StudentEvaluator.Evaluate([Course(11)]).Scholarship);
    }
}
