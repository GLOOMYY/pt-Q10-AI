using System.Globalization;
using System.Text.Json.Nodes;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Domain.Attendance;
using TutorFlow.Domain.Evaluation;
using TutorFlow.Domain.Rules;

namespace TutorFlow.Application.Assistant.Tools;

/// <summary>
/// Turns the rows of the simulated API into the deterministic evaluation of <c>TutorFlow.Domain</c> (R-CAL-09) and
/// into the JSON the tools hand the model: every number is already computed and formatted (API-06, R-QA-05), so the
/// model only writes the answer.
/// </summary>
public static class StudentReport
{
    /// <summary>Text for a value that does not exist in the data (API-10: <c>null</c> is "no data", never 0).</summary>
    public const string NoData = "sin dato";

    /// <summary>
    /// Joins grade and attendance rows by course code, never by name (API-09). A row without its counterpart is kept
    /// with <see langword="null"/> on the missing side, so the domain flags it as inconsistent (R-CAL-12).
    /// </summary>
    /// <param name="grades">Grade rows (<see langword="null"/> if the API had none).</param>
    /// <param name="attendance">Attendance rows (<see langword="null"/> if the API had none).</param>
    /// <returns>The courses, in the order of the grade rows and then attendance-only rows.</returns>
    public static IReadOnlyList<CourseInput> JoinCourses(
        IReadOnlyList<GradeRecord>? grades,
        IReadOnlyList<AttendanceRecord>? attendance)
    {
        grades ??= [];
        attendance ??= [];
        var courses = new List<CourseInput>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var grade in grades)
        {
            if (!seen.Add(grade.CourseCode))
            {
                continue;
            }

            var row = attendance.FirstOrDefault(a => string.Equals(a.CourseCode, grade.CourseCode, StringComparison.Ordinal));
            courses.Add(new CourseInput(
                grade.CourseCode,
                grade.CourseName,
                new CourseGradesInput(grade.Credits, grade.Cut1, grade.Cut2, grade.Cut3),
                row is null ? null : new CourseAttendanceInput(row.ScheduledClasses, row.Absences)));
        }

        foreach (var row in attendance)
        {
            if (seen.Add(row.CourseCode))
            {
                courses.Add(new CourseInput(
                    row.CourseCode, row.CourseName, null, new CourseAttendanceInput(row.ScheduledClasses, row.Absences)));
            }
        }

        return courses;
    }

    /// <summary>Builds the JSON of a student's period for the model.</summary>
    /// <param name="student">The roster entry of the student.</param>
    /// <param name="evaluation">The domain evaluation.</param>
    /// <param name="attendance">The attendance rows, for the counts of absences and scheduled classes.</param>
    /// <returns>The JSON object.</returns>
    public static JsonObject Build(StudentRecord student, StudentEvaluation evaluation, IReadOnlyList<AttendanceRecord>? attendance)
    {
        ArgumentNullException.ThrowIfNull(student);
        ArgumentNullException.ThrowIfNull(evaluation);

        var provisional = (evaluation.Average?.IsProvisional ?? false) || evaluation.HasInconsistentCourses;
        var courses = new JsonArray();
        foreach (var course in evaluation.Courses)
        {
            courses.Add(Course(course, attendance?.FirstOrDefault(a => string.Equals(a.CourseCode, course.Code, StringComparison.Ordinal))));
        }

        return new JsonObject
        {
            ["estudiante"] = new JsonObject
            {
                ["id"] = student.Id,
                ["nombre"] = student.Name,
                ["programa"] = student.ProgramId,
                ["semestre"] = student.Semester is { } semester ? JsonValue.Create(semester) : NoData,
            },
            ["asignaturas"] = courses,
            ["asignaturasPerdidas"] = Names(evaluation.LostCourses),
            ["asignaturasHabilitables"] = Names(evaluation.HabilitableCourses),
            ["creditosMatriculados"] = evaluation.EnrolledCredits,
            ["promedioPeriodo"] = Average(evaluation),
            ["pruebaAcademica"] = new JsonObject
            {
                ["resultado"] = evaluation.Probation switch
                {
                    ProbationStatus.InProbation => "sí: promedio del periodo menor que 3.0",
                    ProbationStatus.NotInProbation => "no",
                    _ => "no determinable",
                },
                ["provisional"] = provisional && evaluation.Probation != ProbationStatus.Undetermined,
            },
            ["perdidaDeCupo"] = "no determinable: requiere dos periodos consecutivos en prueba académica y solo hay datos del periodo actual",
            ["becaRendimiento"] = new JsonObject
            {
                ["resultado"] = evaluation.Scholarship switch
                {
                    ScholarshipStatus.Eligible => "cumple los requisitos",
                    ScholarshipStatus.NotEligible => "no cumple los requisitos",
                    _ => "no determinable todavía",
                },
                ["provisional"] = provisional && evaluation.Scholarship != ScholarshipStatus.Undetermined,
            },
        };
    }

    /// <summary>Finds a course of the evaluation by code or by name (accent and case insensitive).</summary>
    /// <param name="evaluation">The evaluation.</param>
    /// <param name="query">Code or name given by the model.</param>
    /// <returns>The matching courses: exactly one when the query is unambiguous.</returns>
    public static IReadOnlyList<CourseEvaluation> FindCourses(StudentEvaluation evaluation, string? query)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        var wanted = TextNormalizer.Normalize(query);
        if (wanted.Length == 0)
        {
            return [];
        }

        var byCode = evaluation.Courses.Where(c => TextNormalizer.Normalize(c.Code) == wanted).ToList();
        if (byCode.Count > 0)
        {
            return byCode;
        }

        var byName = evaluation.Courses.Where(c => TextNormalizer.Normalize(c.Name) == wanted).ToList();
        if (byName.Count > 0)
        {
            return byName;
        }

        return evaluation.Courses
            .Where(c => TextNormalizer.Normalize(c.Name).Contains(wanted, StringComparison.Ordinal)
                || wanted.Contains(TextNormalizer.Normalize(c.Name), StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>Names of the courses (to tell the model which courses exist).</summary>
    /// <param name="courses">The courses.</param>
    /// <returns>A JSON array of names.</returns>
    public static JsonArray Names(IEnumerable<CourseEvaluation> courses) =>
        [.. courses.Select(c => (JsonNode)JsonValue.Create(c.Name))];

    private static JsonObject Course(CourseEvaluation course, AttendanceRecord? row)
    {
        var json = new JsonObject
        {
            ["codigo"] = course.Code,
            ["asignatura"] = course.Name,
            ["creditos"] = course.Credits is { } credits ? JsonValue.Create(credits) : NoData,
            ["corte1 (30 %)"] = CutText(course.Cut1),
            ["corte2 (30 %)"] = CutText(course.Cut2),
            ["corte3 (40 %)"] = CutText(course.Cut3),
        };

        if (!course.IsConsistent)
        {
            // R-CAL-12: inconsistent data is reported as such and never used in calculations.
            json["estado"] = "dato inconsistente: no se usa en los cálculos y las conclusiones que dependen de ella son provisionales";
            return json;
        }

        json["definitiva"] = course.FinalGrade is { } final ? NumberFormat.Grade(final) : "sin definitiva (falta al menos un corte)";
        if (course.Absences == AbsenceStatus.Lost && course.GradeFromCuts is { } fromCuts)
        {
            json["notaSegunCortes"] = NumberFormat.Grade(fromCuts);
        }

        if (course.Accumulated is { } accumulated)
        {
            json["notaAcumulada"] = NumberFormat.Average(accumulated.Value);
            json["porcentajeEvaluado"] = accumulated.EvaluatedPercent.ToString(CultureInfo.InvariantCulture) + " %";
        }

        json["asistencia"] = Attendance(course, row);
        json["estado"] = course.Outcome switch
        {
            CourseOutcome.Passed => "aprobada",
            CourseOutcome.PassUndetermined => "aprobada por nota, pero la pérdida por inasistencia no es determinable (sin dato de inasistencias)",
            CourseOutcome.Failed => "perdida (definitiva menor que 3.0)",
            CourseOutcome.LostByAbsences => "perdida por inasistencia (definitiva 0.0, sin importar las notas de los cortes)",
            CourseOutcome.NoFinalGrade => "sin definitiva: no cuenta como aprobada ni como perdida y no entra en el promedio",
            _ => "dato inconsistente",
        };
        json["habilitable"] = course.Habilitation switch
        {
            HabilitationStatus.Eligible => "sí",
            HabilitationStatus.Undetermined => "no determinable",
            _ => "no",
        };
        return json;
    }

    private static JsonObject Attendance(CourseEvaluation course, AttendanceRecord? row)
    {
        var json = new JsonObject();
        if (row?.ScheduledClasses is not { } scheduled)
        {
            json["clasesProgramadas"] = NoData;
            return json;
        }

        json["clasesProgramadas"] = scheduled;

        // R-ASI-01: lost with MORE than 15 %, computed with integers: absences x 100 > 15 x scheduled.
        json["pierdeConFaltasDesde"] = (AttendancePolicy.MaxAbsencePercent * scheduled / 100) + 1;
        if (row.Absences is { } absences)
        {
            json["inasistencias"] = absences;
            json["porcentajeInasistencia"] = NumberFormat.Percent(absences * 100m / scheduled);
        }
        else
        {
            // R-ASI-03, API-10: null is unknown, never 0.
            json["inasistencias"] = NoData;
        }

        json["perdidaPorInasistencia"] = course.Absences switch
        {
            AbsenceStatus.Lost => "sí (más del 15 % de las clases programadas)",
            AbsenceStatus.NotLost => "no",
            _ => "no determinable: no hay dato de inasistencias",
        };
        return json;
    }

    private static JsonNode Average(StudentEvaluation evaluation)
    {
        if (evaluation.Average is not { } average)
        {
            // R-CAL-12, D-20: never divide by 0 credits.
            return "sin promedio en el periodo";
        }

        var codes = average.UnknownAbsenceCourseCodes.ToHashSet(StringComparer.Ordinal);
        var inconsistent = average.InconsistentCourseCodes.ToHashSet(StringComparer.Ordinal);
        var json = new JsonObject
        {
            ["valor"] = NumberFormat.Average(average.Value),
            ["creditosIncluidos"] = evaluation.Courses
                .Where(c => c.EffectiveGrade() is not null)
                .Sum(c => c.Credits ?? 0),
            ["provisional"] = average.IsProvisional,
        };

        if (codes.Count > 0)
        {
            json["dependeDeLaAsistenciaDe"] = Names(evaluation.Courses.Where(c => codes.Contains(c.Code)));
            if (average.ValueIfUnknownAbsencesLost is { } alternative)
            {
                json["valorSiEsasAsignaturasSePerdieronPorInasistencia"] = NumberFormat.Average(alternative);
            }
        }

        if (inconsistent.Count > 0)
        {
            json["excluidasPorDatoInconsistente"] = Names(evaluation.Courses.Where(c => inconsistent.Contains(c.Code)));
        }

        return json;
    }

    private static JsonNode CutText(decimal? cut) => cut is { } value ? NumberFormat.Grade(value) : "sin nota";
}
