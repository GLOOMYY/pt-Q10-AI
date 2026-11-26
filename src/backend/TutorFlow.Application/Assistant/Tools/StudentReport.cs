using System.Globalization;
using System.Text.Json.Nodes;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Domain.Attendance;
using TutorFlow.Domain.Evaluation;
using TutorFlow.Domain.Grading;
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

    private const string Regulation = "reglamento_academico_2025.md";
    private const string Welfare = "bienestar_y_becas.md";

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
            ["asignaturasPerdidas"] = LostCourses(evaluation.LostCourses),
            ["habilitacion"] = Habilitation(evaluation),
            ["creditosMatriculados"] = evaluation.EnrolledCredits,
            ["promedioPeriodo"] = Average(evaluation),
            ["pruebaAcademica"] = new JsonObject
            {
                ["resultado"] = evaluation.Probation switch
                {
                    ProbationStatus.InProbation => $"sí: promedio del periodo menor que {NumberFormat.Grade(StudentEvaluator.ProbationThreshold)}",
                    ProbationStatus.NotInProbation => "no",
                    _ => "no determinable",
                },
                ["detalle"] = $"promedio ponderado del periodo: {AverageText(evaluation)}; queda en prueba académica con un promedio menor que {NumberFormat.Grade(StudentEvaluator.ProbationThreshold)}",
                ["provisional"] = provisional && evaluation.Probation != ProbationStatus.Undetermined,
                ["reglaACitar"] = RuleCitation(Regulation, "Artículo 20"),
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
                ["detalle"] = ScholarshipDetail(evaluation),
                ["provisional"] = provisional && evaluation.Scholarship != ScholarshipStatus.Undetermined,
                ["reglaACitar"] = RuleCitation(Welfare, "Sección 3.1"),
            },
        };
    }

    /// <summary>
    /// Deterministic conclusions of a whole program for a coordinator (R-CAL-08, R-BEC-01, R-ASI-01, R-ASI-03): who is
    /// in each situation, with the figure that supports it and the rule to cite, so the answer can name every student.
    /// Only students already authorized and loaded by the caller are included (R-SEG-03).
    /// </summary>
    /// <param name="students">Each authorized student with the evaluation and the attendance rows.</param>
    /// <returns>The JSON object with one entry per situation.</returns>
    public static JsonObject ProgramConclusions(
        IEnumerable<(StudentRecord Student, StudentEvaluation Evaluation, IReadOnlyList<AttendanceRecord>? Attendance)> students)
    {
        ArgumentNullException.ThrowIfNull(students);
        var inProbation = new JsonArray();
        var probationUndetermined = new JsonArray();
        var scholarship = new JsonArray();
        var scholarshipUndetermined = new JsonArray();
        var lost = new JsonArray();
        var lostByAbsences = new JsonArray();
        var absencesUndetermined = new JsonArray();

        foreach (var (student, evaluation, attendance) in students)
        {
            if (evaluation.Probation == ProbationStatus.InProbation)
            {
                inProbation.Add(StudentLine(student, "promedio", AverageText(evaluation)));
            }
            else if (evaluation.Probation == ProbationStatus.Undetermined)
            {
                probationUndetermined.Add(StudentLine(student, "promedio", AverageText(evaluation)));
            }

            if (evaluation.Scholarship == ScholarshipStatus.Eligible)
            {
                scholarship.Add(StudentLine(student, "detalle", ScholarshipDetail(evaluation)));
            }
            else if (evaluation.Scholarship == ScholarshipStatus.Undetermined)
            {
                scholarshipUndetermined.Add(StudentLine(student, "detalle", ScholarshipDetail(evaluation)));
            }

            var lostCourses = evaluation.LostCourses.ToList();
            if (lostCourses.Count > 0)
            {
                var line = StudentLine(student, null, null);
                line["asignaturas"] = LostCourses(lostCourses);
                lost.Add(line);
            }

            var byAbsences = evaluation.Courses.Where(c => c.Absences == AbsenceStatus.Lost).ToList();
            if (byAbsences.Count > 0)
            {
                var line = StudentLine(student, null, null);
                line["asignaturas"] = new JsonArray([.. byAbsences.Select(c => (JsonNode)new JsonObject
                {
                    ["asignatura"] = c.Name,
                    ["inasistencias"] = AbsenceSummary(RowOf(attendance, c.Code)),
                })]);
                lostByAbsences.Add(line);
            }

            var unknown = evaluation.Courses.Where(c => c.IsConsistent && c.Absences == AbsenceStatus.Unknown).ToList();
            if (unknown.Count > 0)
            {
                var line = StudentLine(student, null, null);
                line["asignaturasSinDatoDeInasistencias"] = Names(unknown);
                absencesUndetermined.Add(line);
            }
        }

        return new JsonObject
        {
            ["pruebaAcademica"] = new JsonObject
            {
                ["enPruebaAcademica"] = inProbation,
                ["noDeterminable"] = probationUndetermined,
                ["reglaACitar"] = RuleCitation(Regulation, "Artículo 20"),
            },
            ["becaRendimiento"] = new JsonObject
            {
                ["cumplenLosRequisitos"] = scholarship,
                ["noDeterminableTodavia"] = scholarshipUndetermined,
                ["reglaACitar"] = RuleCitation(Welfare, "Sección 3.1"),
            },
            ["asignaturasPerdidas"] = new JsonObject
            {
                ["estudiantes"] = lost,
                // R-RES-02, ESC-COO-01: a loss by absences in the list is also supported by Artículo 11.
                ["reglasACitar"] = lostByAbsences.Count > 0
                    ? new JsonArray(RuleCitation(Regulation, "Artículo 5"), RuleCitation(Regulation, "Artículo 11"))
                    : new JsonArray(RuleCitation(Regulation, "Artículo 5")),
            },
            ["perdidaPorInasistencia"] = new JsonObject
            {
                ["perdieronPorInasistencia"] = lostByAbsences,
                ["noDeterminableSinDatoDeInasistencias"] = absencesUndetermined,
                ["reglaACitar"] = RuleCitation(Regulation, "Artículo 11"),
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

    /// <summary>
    /// Lost courses with the final grade that makes them lost (ESC-BAN-06): the answer must show the grade, not just
    /// the name (R-RES-02). A course lost by absences shows 0.0 (R-CAL-06).
    /// </summary>
    private static JsonArray LostCourses(IEnumerable<CourseEvaluation> courses) =>
    [
        .. courses.Select(c => (JsonNode)new JsonObject
        {
            ["asignatura"] = c.Name,
            ["definitiva"] = c.FinalGrade is { } final ? NumberFormat.Grade(final) : NoData,
            ["motivo"] = c.Outcome == CourseOutcome.LostByAbsences
                ? "perdida por inasistencia (definitiva 0.0, Artículo 11)"
                : $"definitiva menor que {NumberFormat.Grade(GradingRules.PassingGrade)}",
        }),
    ];

    /// <summary>
    /// Habilitation of the period (R-HAB-01, R-HAB-02): the habilitable courses with the final grade that makes them so,
    /// and the lost courses that are not, with the reason and the rule to cite (Art. 6, and Art. 7 when one is not).
    /// </summary>
    private static JsonObject Habilitation(StudentEvaluation evaluation)
    {
        var habilitable = new JsonArray();
        var notHabilitable = new JsonArray();
        var citesArticle7 = false;
        foreach (var course in evaluation.LostCourses)
        {
            var grade = course.FinalGrade is { } final ? NumberFormat.Grade(final) : NoData;
            if (course.Habilitation == HabilitationStatus.Eligible)
            {
                habilitable.Add(new JsonObject { ["asignatura"] = course.Name, ["definitiva"] = grade });
            }
            else
            {
                // DUDA-42: the reason comes from Domain, so the answer explains exactly why (R-HAB-01, R-HAB-02).
                var reason = course.NotHabilitableReason;
                citesArticle7 |= reason is not NotHabilitableReason.GradeBelowMinimum
                    and not NotHabilitableReason.NoFinalGrade
                    and not NotHabilitableReason.AlreadyPassed;
                notHabilitable.Add(new JsonObject
                {
                    ["asignatura"] = course.Name,
                    ["definitiva"] = grade,
                    ["motivo"] = course.Habilitation == HabilitationStatus.Undetermined
                        ? "no determinable"
                        : NotHabilitableText(reason),
                });
            }
        }

        var rules = new JsonArray { RuleCitation(Regulation, "Artículo 6") };
        if (citesArticle7)
        {
            rules.Add(RuleCitation(Regulation, "Artículo 7"));
        }

        return new JsonObject
        {
            ["habilitables"] = habilitable,
            ["perdidasNoHabilitables"] = notHabilitable,
            ["reglasACitar"] = rules,
        };
    }

    /// <summary>
    /// Spanish explanation of why a course is not habilitable, with the article that says so (R-HAB-01, R-HAB-02,
    /// DUDA-42). <see cref="NotHabilitableReason.None"/> keeps a neutral wording so that it is never false.
    /// </summary>
    /// <param name="reason">The reason given by Domain.</param>
    /// <returns>The text for the model.</returns>
    public static string NotHabilitableText(NotHabilitableReason reason) => reason switch
    {
        NotHabilitableReason.LostByAbsences => "perdida por inasistencia: no es habilitable (Artículo 7)",
        NotHabilitableReason.NonHabilitableType => "práctica profesional o trabajo de grado: no es habilitable (Artículo 7)",
        NotHabilitableReason.GradeBelowMinimum =>
            $"definitiva menor que {NumberFormat.Grade(HabilitationRules.MinimumFinalGrade)}: no es habilitable (Artículo 6)",
        NotHabilitableReason.NoFinalGrade => "sin definitiva todavía: no se puede habilitar",
        NotHabilitableReason.AlreadyPassed => "aprobada: no hay nada que habilitar",
        _ => "no cumple las condiciones de habilitación de los Artículos 6 y 7",
    };

    private static string AverageText(StudentEvaluation evaluation) => evaluation.Average is { } average
        ? NumberFormat.Average(average.Value) + (average.IsProvisional ? " (provisional)" : string.Empty)
        : "sin promedio en el periodo";

    private static string ScholarshipDetail(StudentEvaluation evaluation) =>
        $"promedio ponderado del periodo: {AverageText(evaluation)} (se requiere {NumberFormat.Grade(StudentEvaluator.ScholarshipMinimumAverage)} o más); "
        + $"asignaturas perdidas: {evaluation.LostCourses.Count()} (se requiere ninguna); "
        + $"créditos matriculados: {evaluation.EnrolledCredits.ToString(CultureInfo.InvariantCulture)} (se requieren al menos {StudentEvaluator.ScholarshipMinimumCredits.ToString(CultureInfo.InvariantCulture)}); "
        + $"asignaturas sin definitiva: {evaluation.Courses.Count(c => c.Outcome == CourseOutcome.NoFinalGrade)}";

    private static JsonObject StudentLine(StudentRecord student, string? field, string? value)
    {
        var json = new JsonObject { ["id"] = student.Id, ["nombre"] = student.Name };
        if (field is not null)
        {
            json[field] = value;
        }

        return json;
    }

    private static AttendanceRecord? RowOf(IReadOnlyList<AttendanceRecord>? attendance, string code) =>
        attendance?.FirstOrDefault(a => string.Equals(a.CourseCode, code, StringComparison.Ordinal));

    private static string AbsenceSummary(AttendanceRecord? row) =>
        row is { ScheduledClasses: { } scheduled, Absences: { } absences } && scheduled > 0
            ? $"{absences} de {scheduled} clases programadas ({NumberFormat.Percent(absences * 100m / scheduled)})"
            : NoData;

    private static JsonObject RuleCitation(string document, string section) =>
        new() { ["documento"] = document, ["seccion"] = section };

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
            CourseOutcome.Failed => $"perdida (definitiva menor que {NumberFormat.Grade(GradingRules.PassingGrade)})",
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
            json["resumen"] = AbsenceSummary(row);
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

            // ESC-BAN-02, R-RES-02: the answer shows the final grades and credits the average was computed with.
            ["desglose"] = Breakdown(evaluation),
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

    /// <summary>
    /// The courses that enter the average, each with the grade used (0.0 if lost by absences, R-CAL-06) and its
    /// credits (R-CAL-04). Courses without a final grade or with inconsistent data are left out (R-CAL-05, R-CAL-12).
    /// </summary>
    private static JsonArray Breakdown(StudentEvaluation evaluation) =>
    [
        .. evaluation.Courses
            .Where(c => c.EffectiveGrade() is not null)
            .Select(c => (JsonNode)new JsonObject
            {
                ["asignatura"] = c.Name,
                ["definitiva"] = NumberFormat.Grade(c.EffectiveGrade()!.Value),
                ["creditos"] = c.Credits is { } credits ? JsonValue.Create(credits) : NoData,
            }),
    ];

    private static JsonNode CutText(decimal? cut) => cut is { } value ? NumberFormat.Grade(value) : "sin nota";
}
