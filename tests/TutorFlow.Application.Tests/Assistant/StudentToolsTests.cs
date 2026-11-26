using System.Text.Json;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Tests.Support;
using TutorFlow.Testing.Llm;
using Xunit;

namespace TutorFlow.Application.Tests.Assistant;

/// <summary>
/// The student's tools (R-ARQ-01..02, R-SEG-02, R-SEG-06): identity from the session, deterministic results already
/// formatted (API-06, R-QA-05), usoDatosEstudiante (R-RES-04), data citations (R-RES-02) and the consulted students in
/// the log (R-LOG-04, R-LOG-06). What the LLM writes is not checked here (escenarios/README §4.4).
/// </summary>
public class StudentToolsTests
{
    private const string Notas = "Notas · periodo 2026-2";
    private const string Asistencia = "Asistencia · periodo 2026-2";

    private static readonly string[] TwoCourses = ["Programación Orientada a Objetos", "Bases de Datos"];

    private static readonly string[] Est001FinalGrades = ["4.0", "4.1", "4.5", "4.8"];

    private static string Text(JsonElement element, string property) => element.GetProperty(property).GetString()!;

    private static JsonElement Course(JsonElement report, string code) =>
        report.GetProperty("asignaturas").EnumerateArray().Single(c => Text(c, "codigo") == code);

    // ESC-BAN-01: answered from the documents only: no student data, empty log of students.
    [Fact]
    public async Task Document_only_answer_does_not_use_student_data()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "nota mínima" }))
            .EnqueueText(AssistantHarness.Final("La nota mínima es 3.0.", ("reglamento_academico_2025.md", "Artículo 5")));

        var answer = await h.AskStudentAsync("¿Cuál es la nota mínima para aprobar una asignatura?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.Equal([new Citation("reglamento_academico_2025.md", "Artículo 5")], answer.Citations);
        Assert.False(answer.UsedStudentData);
        Assert.Null(answer.Reason);
        Assert.Empty(h.Log.StudentsConsulted);
        Assert.Empty(h.StudentDataCalls);
        Assert.Equal([AssistantTools.SearchRegulations], h.Log.Tools);

        // R-RES-05: tokens are the sum of totalTokenCount of both calls; cost rounded to 6 decimals.
        Assert.Equal(2 * (FakeLlmReply.DefaultPromptTokens + FakeLlmReply.DefaultCandidatesTokens), answer.Tokens);
        Assert.Equal(0.000110m, answer.CostUsd);
    }

    // ESC-BAN-02, CL-07: EST-001 average computed by the backend (4.39), identity from the session.
    [Fact]
    public async Task Own_academic_status_is_computed_formatted_and_logged()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final(
                "Tu promedio es 4.39.",
                ("evaluacion_y_calificaciones.md", "Sección 3"),
                ("evaluacion_y_calificaciones.md", "Sección 4"),
                ("Datos del estudiante", Notas)));

        var answer = await h.AskStudentAsync("¿Cuál es mi promedio ponderado del periodo?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.True(answer.UsedStudentData);
        Assert.Equal(["EST-001"], h.Log.StudentsConsulted);
        Assert.Equal(["/estudiantes/EST-001/notas", "/estudiantes/EST-001/asistencia"], h.StudentDataCalls);
        Assert.Contains("/salud", h.Log.ResourcesConsulted);
        Assert.Equal(
            [
                new Citation("evaluacion_y_calificaciones.md", "Sección 3"),
                new Citation("evaluacion_y_calificaciones.md", "Sección 4"),
                new Citation("Datos del estudiante", Notas),
            ],
            answer.Citations);

        var report = h.ToolResult();
        Assert.Equal("4.39", Text(report.GetProperty("promedioPeriodo"), "valor"));
        Assert.False(report.GetProperty("promedioPeriodo").GetProperty("provisional").GetBoolean());
        Assert.Equal("4.5", Text(Course(report, "CAL-201"), "definitiva"));

        // API-06: a grade of 4 is shown as 4.0.
        Assert.Equal("4.0", Text(Course(report, "ING-204"), "definitiva"));
        Assert.Equal("cumple los requisitos", Text(report.GetProperty("becaRendimiento"), "resultado"));
        Assert.Equal(
            [Notas, Asistencia],
            report.GetProperty("citasDatos").EnumerateArray().Select(c => Text(c, "seccion")));
    }

    // ESC-BAN-02, R-RES-02: the average carries the final grades and credits it was computed with (4.5, 4.8, 4.1, 4.0).
    [Fact]
    public async Task Average_reports_the_breakdown_of_final_grades_and_credits()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final("Tu promedio es 4.39.", ("Datos del estudiante", Notas)));

        await h.AskStudentAsync("¿Cuál es mi promedio ponderado del periodo?");

        var breakdown = h.ToolResult().GetProperty("promedioPeriodo").GetProperty("desglose").EnumerateArray().ToList();
        Assert.Equal(
            Est001FinalGrades,
            breakdown.Select(c => Text(c, "definitiva")).Order(StringComparer.Ordinal));
        Assert.All(breakdown, c => Assert.True(c.GetProperty("creditos").GetInt32() > 0));
    }

    // ESC-BAN-06, ESC-EST-04: a lost course is reported with its final grade, not only its name.
    [Fact]
    public async Task Lost_courses_carry_their_final_grade()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final("Perdiste Microeconomía (1.8) y Estadística (2.3).", ("Datos del estudiante", Notas)));

        await h.AskStudentAsync("¿Qué asignaturas perdí?", "EST-005");

        var lost = h.ToolResult().GetProperty("asignaturasPerdidas").EnumerateArray()
            .ToDictionary(c => Text(c, "asignatura"), c => Text(c, "definitiva"));
        Assert.Equal("1.8", lost["Microeconomía"]);
        Assert.Equal("2.3", lost["Estadística"]);
    }

    // ESC-EST-01, CL-03, R-CAL-06: a course lost by absences is listed as lost with final grade 0.0 and its reason.
    [Fact]
    public async Task Course_lost_by_absences_is_listed_with_zero_and_its_reason()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final("Perdiste Bases de Datos por inasistencia (0.0).", ("Datos del estudiante", Notas)));

        await h.AskStudentAsync("¿Qué asignaturas perdí?", "EST-002");

        var databases = h.ToolResult().GetProperty("asignaturasPerdidas").EnumerateArray()
            .Single(c => Text(c, "asignatura") == "Bases de Datos");
        Assert.Equal("0.0", Text(databases, "definitiva"));
        Assert.Contains("inasistencia", Text(databases, "motivo"), StringComparison.Ordinal);
    }

    // ESC-ENT-18, API-05: the id is normalized and the session id is used by the tools.
    [Fact]
    public async Task Normalized_identity_is_the_one_the_tools_use()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final("Tu promedio es 4.39.", ("Datos del estudiante", Notas)));

        var answer = await h.AskStudentAsync("¿Cuál es mi promedio ponderado?", " est-001 ");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.True(answer.UsedStudentData);
        Assert.Equal(["EST-001"], h.Log.StudentsConsulted);
    }

    // ESC-EST-01, CL-03: lost by absences → 0.0 whatever the cuts; 9/48 = 18.75 %; average 2.28 and probation.
    [Fact]
    public async Task Course_lost_by_absences_and_probation_come_from_the_domain()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final("Bases de Datos: 0.0.", ("reglamento_academico_2025.md", "Artículo 11")));

        await h.AskStudentAsync("¿Cuál es mi nota definitiva en Bases de Datos?", "EST-002");

        var report = h.ToolResult();
        var databases = Course(report, "BD-203");
        Assert.Equal("0.0", Text(databases, "definitiva"));
        Assert.Equal("3.8", Text(databases, "notaSegunCortes"));
        Assert.Equal("18.75 %", Text(databases.GetProperty("asistencia"), "porcentajeInasistencia"));
        Assert.Equal(8, databases.GetProperty("asistencia").GetProperty("pierdeConFaltasDesde").GetInt32());
        Assert.Equal("no", Text(databases, "habilitable"));
        Assert.Equal("sí", Text(Course(report, "CAL-201"), "habilitable"));
        Assert.Equal("2.28", Text(report.GetProperty("promedioPeriodo"), "valor"));
        Assert.StartsWith("sí", Text(report.GetProperty("pruebaAcademica"), "resultado"), StringComparison.Ordinal);
        Assert.StartsWith("no determinable", Text(report, "perdidaDeCupo"), StringComparison.Ordinal);
    }

    // ESC-EST-02, ESC-EST-03, ESC-BAN-03, R-RES-02: the conclusions carry the figures and the rule that support them.
    [Fact]
    public async Task Conclusions_carry_their_figures_and_rules()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final("Puedes habilitar Cálculo Integral (2.6).", ("Datos del estudiante", Notas)));

        await h.AskStudentAsync("¿Qué materias puedo habilitar?", "EST-002");

        var report = h.ToolResult();
        var habilitation = report.GetProperty("habilitacion");
        var habilitable = Assert.Single(habilitation.GetProperty("habilitables").EnumerateArray());
        Assert.Equal("Cálculo Integral", Text(habilitable, "asignatura"));
        Assert.Equal("2.6", Text(habilitable, "definitiva"));
        var databases = Assert.Single(habilitation.GetProperty("perdidasNoHabilitables").EnumerateArray());
        Assert.Equal("Bases de Datos", Text(databases, "asignatura"));
        Assert.Contains("Artículo 7", Text(databases, "motivo"), StringComparison.Ordinal);
        Assert.Equal(
            ["Artículo 6", "Artículo 7"],
            habilitation.GetProperty("reglasACitar").EnumerateArray().Select(c => Text(c, "seccion")));

        var probation = report.GetProperty("pruebaAcademica");
        Assert.Contains("2.28", Text(probation, "detalle"), StringComparison.Ordinal);
        Assert.Equal("Artículo 20", Text(probation.GetProperty("reglaACitar"), "seccion"));
        Assert.Equal("Sección 3.1", Text(report.GetProperty("becaRendimiento").GetProperty("reglaACitar"), "seccion"));
        Assert.Equal(
            "9 de 48 clases programadas (18.75 %)",
            Text(Course(report, "BD-203").GetProperty("asistencia"), "resumen"));
    }

    // ESC-EST-04, DUDA-42, R-HAB-01: a course lost with a final grade below 2.0 says why it is not habilitable, citing
    // only Artículo 6 (Artículo 7 is for absences, internships and degree projects).
    [Fact]
    public async Task Not_habilitable_reason_comes_from_the_domain()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final("Microeconomía no es habilitable (1.8).", ("Datos del estudiante", Notas)));

        await h.AskStudentAsync("¿Puedo habilitar Microeconomía?", "EST-005");

        var habilitation = h.ToolResult().GetProperty("habilitacion");
        var micro = Assert.Single(habilitation.GetProperty("perdidasNoHabilitables").EnumerateArray());
        Assert.Equal("Microeconomía", Text(micro, "asignatura"));
        Assert.Equal("definitiva menor que 2.0: no es habilitable (Artículo 6)", Text(micro, "motivo"));
        Assert.Equal(
            ["Artículo 6"],
            habilitation.GetProperty("reglasACitar").EnumerateArray().Select(c => Text(c, "seccion")));
    }

    // ESC-SIM-08, R-RES-02: the simulation summary shows the accumulated grade it starts from.
    [Fact]
    public async Task Simulation_summary_shows_the_accumulated_grade()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SimulateRequiredGrade, new { asignatura = "Estadística" }))
            .EnqueueText(AssistantHarness.Final("Necesitas 1.4.", ("Datos del estudiante", Notas)));

        await h.AskStudentAsync("¿Cuánto necesito en el corte 3 de Estadística para pasar?", "EST-006");

        var summary = Text(h.ToolResult(), "resumen");
        Assert.Contains("2.40", summary, StringComparison.Ordinal);
        Assert.Contains("1.4", summary, StringComparison.Ordinal);
    }

    // ESC-EST-08, CL-26, R-CAL-10: provisional average 4.11, 3.34 if Inglés III was lost by absences.
    [Fact]
    public async Task Provisional_average_reports_its_alternative()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final("Tu promedio provisional es 4.11.", ("Datos del estudiante", Notas)));

        await h.AskStudentAsync("¿Cuál es mi promedio del periodo?", "EST-003");

        var average = h.ToolResult().GetProperty("promedioPeriodo");
        Assert.Equal("4.11", Text(average, "valor"));
        Assert.True(average.GetProperty("provisional").GetBoolean());
        Assert.Equal("3.34", Text(average, "valorSiEsasAsignaturasSePerdieronPorInasistencia"));
        Assert.Equal(["Inglés III"], average.GetProperty("dependeDeLaAsistenciaDe").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(StudentReport.NoData, Text(Course(h.ToolResult(), "ING-204").GetProperty("asistencia"), "inasistencias"));
    }

    // ESC-COO-10, R-CAL-10, CL-26: the provisional average comes with a sentence that gives both values.
    [Fact]
    public async Task Provisional_average_summary_gives_both_values()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final("4.11.", ("Datos del estudiante", Notas)));

        await h.AskStudentAsync("¿Cuál es mi promedio del periodo?", "EST-003");

        var summary = Text(h.ToolResult().GetProperty("promedioPeriodo"), "resumen");
        Assert.Contains("4.11", summary, StringComparison.Ordinal);
        Assert.Contains("sería 3.34 si Inglés III se perdió por inasistencia", summary, StringComparison.Ordinal);
        Assert.Contains("3.34", Text(h.ToolResult().GetProperty("pruebaAcademica"), "detalle"), StringComparison.Ordinal);
    }

    // ESC-SIM-09, R-CAL-11, R-RES-02: an unreachable target answer that forgets Sección 3 gets it from the simulation.
    [Fact]
    public async Task Unreachable_simulation_keeps_both_evaluation_sections()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SimulateRequiredGrade, new { asignatura = "Estadística", meta = 5.0 }))
            .EnqueueText(AssistantHarness.Final(
                "La meta de 5.0 ya no es alcanzable: haría falta más de 5.0 en el corte 3. Es una proyección.",
                ("Datos del estudiante", Notas),
                ("evaluacion_y_calificaciones.md", "Sección 2")));

        var answer = await h.AskStudentAsync("¿Cuánto necesito en el corte 3 de Estadística para sacar 5.0 de definitiva?", "EST-006");

        Assert.Contains(new Citation("evaluacion_y_calificaciones.md", "Sección 2"), answer.Citations);
        Assert.Contains(new Citation("evaluacion_y_calificaciones.md", "Sección 3"), answer.Citations);
    }

    // ESC-EST-06, ESC-EST-07, CL-05, R-QA-05: accumulated grade with 2 decimals; average over 8 credits.
    [Fact]
    public async Task Course_without_final_grade_reports_accumulated_with_two_decimals()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final("Acumulada 2.40.", ("Datos del estudiante", Notas)));

        await h.AskStudentAsync("¿Cuál es mi nota en Estadística?", "EST-006");

        var report = h.ToolResult();
        var statistics = Course(report, "EST-103");
        Assert.Equal("2.40", Text(statistics, "notaAcumulada"));
        Assert.Equal("60 %", Text(statistics, "porcentajeEvaluado"));
        Assert.Equal("sin nota", Text(statistics, "corte3 (40 %)"));
        Assert.Equal("4.05", Text(report.GetProperty("promedioPeriodo"), "valor"));
        Assert.Equal(8, report.GetProperty("promedioPeriodo").GetProperty("creditosIncluidos").GetInt32());
        Assert.Equal("no determinable todavía", Text(report.GetProperty("becaRendimiento"), "resultado"));
    }

    // ESC-SIM-08, CL-28: 1.4, not 1.5.
    [Fact]
    public async Task Simulation_uses_the_rounding_rule()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SimulateRequiredGrade, new { asignatura = "Estadística" }))
            .EnqueueText(AssistantHarness.Final(
                "Necesitas 1.4.",
                ("Datos del estudiante", Notas),
                ("evaluacion_y_calificaciones.md", "Sección 2"),
                ("evaluacion_y_calificaciones.md", "Sección 3")));

        var answer = await h.AskStudentAsync("¿Cuánto necesito en el corte 3 de Estadística para pasar?", "EST-006");

        var result = h.ToolResult();
        Assert.Equal("1.4", Text(result, "notaMinimaEnCadaCortePendiente"));
        Assert.Equal("2.40", Text(result, "notaAcumulada"));
        Assert.Equal("3.0", Text(result, "meta"));

        // R-RES-02, R-CAL-11: data plus the rule applied (Evaluación §2-3).
        Assert.Equal(
            ["Sección 2", "Sección 3"],
            result.GetProperty("citasReglas").EnumerateArray().Select(c => Text(c, "seccion")));
        Assert.True(answer.UsedStudentData);
        Assert.Equal(["EST-006"], h.Log.StudentsConsulted);
    }

    // ESC-SIM-09: target 5.0 unreachable; ESC-SIM-10: lost by absences; ESC-SIM-11: already has a final grade.
    [Theory]
    [InlineData("EST-006", "Estadística", 5.0, "ya no es alcanzable")]
    [InlineData("EST-002", "Bases de Datos", 3.0, "se perdió por inasistencia")]
    [InlineData("EST-002", "Cálculo Integral", 3.0, "ya tiene definitiva 2.6")]
    public async Task Simulation_special_outcomes(string student, string course, double target, string expected)
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SimulateRequiredGrade, new { asignatura = course, meta = target }))
            .EnqueueText(AssistantHarness.Final("Proyección.", ("evaluacion_y_calificaciones.md", "Sección 3")));

        await h.AskStudentAsync("¿Cuánto necesito?", student);

        Assert.Contains(expected, Text(h.ToolResult(), "resultado"), StringComparison.Ordinal);
    }

    // ESC-EST-14, CL-15: cancelling two courses of 3 credits leaves 6 < 8.
    [Fact]
    public async Task Cancellation_checks_the_minimum_credits()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(
                AssistantTools.CheckCancellation,
                new { asignaturas = TwoCourses }))
            .EnqueueText(AssistantHarness.Final("No puedes.", ("reglamento_academico_2025.md", "Artículo 16")));

        var answer = await h.AskStudentAsync("¿Puedo cancelar Programación Orientada a Objetos y Bases de Datos?");

        var result = h.ToolResult();
        Assert.Equal(12, result.GetProperty("creditosMatriculados").GetInt32());
        Assert.Equal(6, result.GetProperty("creditosRestantes").GetInt32());
        Assert.False(result.GetProperty("conservaElMinimoDe8Creditos").GetBoolean());
        Assert.True(answer.UsedStudentData);
    }

    // ESC-EST-17, CL-32, R-HAB-04: 4.5 in the habilitation leaves 3.0; no student data needed.
    [Fact]
    public async Task Habilitation_grade_is_capped_at_three()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.ApplyHabilitationGrade, new { notaExamen = 4.5 }))
            .EnqueueText(AssistantHarness.Final("Quedas con 3.0.", ("reglamento_academico_2025.md", "Artículo 6")));

        var answer = await h.AskStudentAsync("Si saco 4.5 en la habilitación de Cálculo Integral, ¿con cuánto quedo?", "EST-002");

        Assert.Equal("3.0", Text(h.ToolResult(), "definitivaResultante"));
        Assert.False(answer.UsedStudentData);
    }

    // R-SEG-06, R-SEG-02: the model asking a student tool for another id gets "not authorized" and the answer is 403.
    [Theory]
    [InlineData("EST-002")]
    [InlineData("Andrés")]
    public async Task Student_tool_for_another_id_is_403(string otherId)
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus, new { estudianteId = otherId }));

        var answer = await h.AskStudentAsync("¿Cuál es mi promedio?");

        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.Equal([AssistantAnswer.ForbiddenCitation], answer.Citations);
        Assert.Equal(AuditReasons.ThirdPartyData, answer.Reason);
        Assert.False(answer.UsedStudentData);
        Assert.Equal(FakeLlmReply.DefaultPromptTokens + FakeLlmReply.DefaultCandidatesTokens, answer.Tokens);
        Assert.Empty(h.StudentDataCalls);
        Assert.Empty(h.Log.StudentsConsulted);
        Assert.Equal(1, h.Llm.CallCount);
    }

    // §4, ESC-EST-22: a student never gets program lists, even if the model calls the coordinator's tool.
    [Theory]
    [InlineData(AssistantTools.ListProgramStudents)]
    [InlineData(AssistantTools.ProgramSummary)]
    [InlineData(AssistantTools.FindStudent)]
    [InlineData(AssistantTools.MyPrograms)]
    public async Task Coordinator_tools_are_403_for_a_student(string tool)
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(tool, new { nombre = "Andrés" }));

        var answer = await h.AskStudentAsync("¿Quiénes son mis compañeros de Ingeniería de Sistemas?");

        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.Equal(AuditReasons.ThirdPartyData, answer.Reason);
        Assert.Empty(h.Log.StudentsConsulted);
        Assert.DoesNotContain(h.Api.Calls, c => c.EndsWith("/notas", StringComparison.Ordinal));
    }

    // The student's tools never expose an id parameter to the model (R-SEG-06).
    [Fact]
    public void Student_tool_definitions_take_no_identity()
    {
        foreach (var tool in AssistantTools.StudentDefinitions)
        {
            Assert.DoesNotContain("estudianteId", tool.ParametersSchema.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain(tool.Name, AssistantTools.CoordinatorOnly);
        }
    }
}
