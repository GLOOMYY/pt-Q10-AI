using System.Text.Json;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Tests.Support;
using TutorFlow.Testing.Llm;
using Xunit;

namespace TutorFlow.Application.Tests.Assistant;

/// <summary>
/// The coordinator's tools: authorization by the rosters of the programs in charge without consulting the requested
/// student (R-SEG-03, CL-10), "Datos del programa" citations (R-RES-02), ambiguous names (R-SEG-12), "own" data
/// (R-SEG-11) and the consulted students in the log (R-LOG-06, D-24).
/// </summary>
public class CoordinatorToolsTests
{
    private static readonly string[] StatisticsOnly = ["Estadística"];

    private static string Text(JsonElement element, string property) => element.GetProperty(property).GetString()!;

    // ESC-COO-07, R-SEG-03: a student of the program is authorized through the roster.
    [Fact]
    public async Task Student_of_own_program_is_authorized_by_the_roster()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus, new { estudianteId = "est-005" }))
            .EnqueueText(AssistantHarness.Final(
                "Microeconomía: 1.8.",
                ("Datos del programa", "ADM · periodo 2026-2"),
                ("evaluacion_y_calificaciones.md", "Sección 3")));

        var answer = await h.AskCoordinatorAsync("¿Cuál es la definitiva de Valentina Castro en Microeconomía?", "COORD-02");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.True(answer.UsedStudentData);
        Assert.Equal(["EST-005"], h.Log.StudentsConsulted);
        Assert.Equal(
            [
                new Citation("Datos del programa", "ADM · periodo 2026-2"),
                new Citation("evaluacion_y_calificaciones.md", "Sección 3"),
            ],
            answer.Citations);

        var micro = h.ToolResult().GetProperty("asignaturas").EnumerateArray().Single(c => Text(c, "codigo") == "MIC-102");
        Assert.Equal("1.8", Text(micro, "definitiva"));
        Assert.Equal("no", Text(micro, "habilitable"));
        Assert.Equal(["/coordinadores/COORD-02", "/programas/ADM/estudiantes"], h.Api.Calls.Take(2));
    }

    // CL-10, ESC-SIM-14, R-SEG-03: a student of another program is a 403 decided without consulting that student.
    [Theory]
    [InlineData(AssistantTools.AcademicStatus)]
    [InlineData(AssistantTools.SimulateRequiredGrade)]
    [InlineData(AssistantTools.CheckCancellation)]
    public async Task Student_of_another_program_is_403_without_consulting_them(string tool)
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(tool, new { estudianteId = "EST-006", asignatura = "Estadística", asignaturas = StatisticsOnly }));

        var answer = await h.AskCoordinatorAsync("¿Cuánto necesita EST-006 en el corte 3 de Estadística para aprobar?");

        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.Equal(AuditReasons.ThirdPartyData, answer.Reason);
        Assert.Equal([AssistantAnswer.ForbiddenCitation], answer.Citations);
        Assert.False(answer.UsedStudentData);
        Assert.Empty(h.Log.StudentsConsulted);
        Assert.Empty(h.StudentDataCalls);
        Assert.Equal(["/coordinadores/COORD-01", "/programas/ING-SIS/estudiantes"], h.Api.Calls);
    }

    // ESC-COO-19: a nonexistent student is the same 403, never confirmed nor denied (R-SEG-05).
    [Fact]
    public async Task Nonexistent_student_is_403()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus, new { estudianteId = "EST-099" }));

        var answer = await h.AskCoordinatorAsync("¿Cuáles son las notas de EST-099?");

        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.Empty(h.StudentDataCalls);
    }

    // ESC-COO-16: a program not in charge is a 403.
    [Theory]
    [InlineData(AssistantTools.ListProgramStudents)]
    [InlineData(AssistantTools.ProgramSummary)]
    public async Task Program_not_in_charge_is_403(string tool)
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(tool, new { programaId = "ADM" }));

        var answer = await h.AskCoordinatorAsync("¿Qué estudiantes de Administración de Empresas tienen materias perdidas?");

        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.DoesNotContain("/programas/ADM/estudiantes", h.Api.Calls);
    }

    // ESC-BAN-06, ESC-COO-03: the program summary consults every student of the program (and only them).
    [Fact]
    public async Task Program_summary_consults_every_student_of_the_program()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.ProgramSummary))
            .EnqueueText(AssistantHarness.Final("Valentina Castro.", ("reglamento_academico_2025.md", "Artículo 5")));

        var answer = await h.AskCoordinatorAsync("¿Qué estudiantes de mi programa tienen alguna asignatura perdida?", "COORD-02");

        Assert.True(answer.UsedStudentData);
        Assert.Equal(["EST-004", "EST-005", "EST-006"], h.Log.StudentsConsulted);
        Assert.DoesNotContain("/programas/ING-SIS/estudiantes", h.Api.Calls);

        // R-RES-02: the program data citation is added when the model forgot it.
        Assert.Equal(
            [
                new Citation("reglamento_academico_2025.md", "Artículo 5"),
                new Citation("Datos del programa", "ADM · periodo 2026-2"),
            ],
            answer.Citations);

        var students = h.ToolResult().GetProperty("estudiantes").EnumerateArray().ToList();
        Assert.Equal(["EST-004", "EST-005", "EST-006"], students.Select(s => Text(s.GetProperty("estudiante"), "id")));
        Assert.Equal("2.65", Text(students[1].GetProperty("promedioPeriodo"), "valor"));
    }

    // ESC-COO-02, ESC-COO-04, ESC-COO-06: the program conclusions name every student of each situation with the figure
    // and the rule to cite, including the undetermined cases (R-ASI-03).
    [Fact]
    public async Task Program_summary_lists_the_conclusions_of_the_program()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.ProgramSummary))
            .EnqueueText(AssistantHarness.Final("Andrés Martínez (2.28).", ("Datos del programa", "ING-SIS · periodo 2026-2")));

        await h.AskCoordinatorAsync("¿Qué estudiantes de mi programa están en prueba académica?");

        var conclusions = h.ToolResult().GetProperty("conclusionesDelPrograma");
        var probation = Assert.Single(conclusions.GetProperty("pruebaAcademica").GetProperty("enPruebaAcademica").EnumerateArray());
        Assert.Equal("Andrés Martínez", Text(probation, "nombre"));
        Assert.Equal("2.28", Text(probation, "promedio"));

        var scholarship = Assert.Single(conclusions.GetProperty("becaRendimiento").GetProperty("cumplenLosRequisitos").EnumerateArray());
        Assert.Equal("Laura Gómez", Text(scholarship, "nombre"));
        Assert.Contains("4.39", Text(scholarship, "detalle"), StringComparison.Ordinal);
        Assert.Equal("Sección 3.1", Text(conclusions.GetProperty("becaRendimiento").GetProperty("reglaACitar"), "seccion"));

        var absences = conclusions.GetProperty("perdidaPorInasistencia");
        var lost = Assert.Single(absences.GetProperty("perdieronPorInasistencia").EnumerateArray());
        Assert.Equal("Andrés Martínez", Text(lost, "nombre"));
        var course = Assert.Single(lost.GetProperty("asignaturas").EnumerateArray());
        Assert.Equal("Bases de Datos", Text(course, "asignatura"));
        Assert.Equal("9 de 48 clases programadas (18.75 %)", Text(course, "inasistencias"));
        var undetermined = Assert.Single(absences.GetProperty("noDeterminableSinDatoDeInasistencias").EnumerateArray());
        Assert.Equal("Sofía Herrera", Text(undetermined, "nombre"));
        Assert.Equal("Artículo 11", Text(absences.GetProperty("reglaACitar"), "seccion"));
    }

    // ESC-COO-11, R-RES-04, D-24: listing the program used to answer is student data, but consults no student.
    [Fact]
    public async Task Listing_the_program_uses_data_but_consults_no_student()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.ListProgramStudents))
            .EnqueueText(AssistantHarness.Final("Laura, Andrés y Sofía."));

        var answer = await h.AskCoordinatorAsync("¿Qué estudiantes hay en mi programa?");

        Assert.True(answer.UsedStudentData);
        Assert.Empty(h.Log.StudentsConsulted);
        Assert.Empty(h.StudentDataCalls);
        Assert.Equal([new Citation("Datos del programa", "ING-SIS · periodo 2026-2")], answer.Citations);
        Assert.Equal(3, h.ToolResult().GetProperty("estudiantes").GetArrayLength());
    }

    // ESC-COO-12, R-SEG-11: "my average" for a coordinator is a 200 without student data.
    [Fact]
    public async Task Coordinator_asking_for_own_grades_is_answered_without_data()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final("Tu rol no tiene datos académicos propios."));

        var answer = await h.AskCoordinatorAsync("¿Cuál es mi promedio?", "COORD-02");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.False(answer.UsedStudentData);
        Assert.Empty(answer.Citations);
        Assert.Empty(h.Log.StudentsConsulted);
    }

    // ESC-COO-12, R-RES-04, R-LOG-04: a roster listed for "my average" is not used to answer, so it is neither data
    // used nor cited; the log still keeps the tool and the listing really made.
    [Fact]
    public async Task Roster_listed_for_own_average_is_not_data_used()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.ListProgramStudents))
            .EnqueueText(AssistantHarness.Final(
                "Como coordinador, tu rol no tiene datos académicos propios.",
                ("Datos del programa", "ADM · periodo 2026-2")));

        var answer = await h.AskCoordinatorAsync("¿Cuál es mi promedio?", "COORD-02");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.False(answer.UsedStudentData);
        Assert.Empty(answer.Citations);
        Assert.Empty(h.Log.StudentsConsulted);
        Assert.Contains(AssistantTools.ListProgramStudents, h.Log.Tools);
        Assert.Contains("/programas/ADM/estudiantes", h.Log.ResourcesConsulted);
    }

    // ESC-COO-11, R-RES-04: a roster whose students the answer names is used, whatever the question says.
    [Fact]
    public async Task Roster_named_in_the_answer_is_data_used()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.ListProgramStudents))
            .EnqueueText(AssistantHarness.Final("Laura Gómez (EST-001)."));

        var answer = await h.AskCoordinatorAsync("¿Quién está en segundo?");

        Assert.True(answer.UsedStudentData);
        Assert.Equal([new Citation("Datos del programa", "ING-SIS · periodo 2026-2")], answer.Citations);
    }

    // ESC-COO-13, R-SEG-12, D-21: a first name alone is ambiguous: matches of own programs only, no student chosen.
    [Theory]
    [InlineData("Laura", true)]
    [InlineData("sofia herrera", false)]
    public async Task Find_student_by_name_never_picks_an_ambiguous_one(string name, bool ambiguous)
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.FindStudent, new { nombre = name }))
            .EnqueueText(AssistantHarness.Final("Indica el ID del estudiante."));

        await h.AskCoordinatorAsync($"¿Cómo va {name}?");

        var result = h.ToolResult();
        Assert.Equal(ambiguous, result.GetProperty("ambiguo").GetBoolean());
        Assert.Single(result.GetProperty("coincidencias").EnumerateArray());
        Assert.Empty(h.Log.StudentsConsulted);
    }

    // R-SEG-05: a name outside the coordinator's programs is neither confirmed nor denied.
    [Theory]
    [InlineData("Valentina Castro")]
    [InlineData("Pedro Pérez")]
    public async Task Find_student_outside_own_programs_is_403(string name)
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.FindStudent, new { nombre = name }));

        var answer = await h.AskCoordinatorAsync($"¿Cómo va {name}?");

        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.DoesNotContain("/programas/ADM/estudiantes", h.Api.Calls);
    }

    // ESC-SIM-13: a coordinator simulates for a student of their program.
    [Fact]
    public async Task Coordinator_simulation_for_own_student()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(
                AssistantTools.SimulateRequiredGrade, new { estudianteId = "EST-006", asignatura = "EST-103" }))
            .EnqueueText(AssistantHarness.Final("Necesita 1.4.", ("Datos del programa", "ADM · periodo 2026-2")));

        var answer = await h.AskCoordinatorAsync("¿Cuánto necesita EST-006 en el corte 3 de Estadística para aprobar?", "COORD-02");

        Assert.Equal("1.4", Text(h.ToolResult(), "notaMinimaEnCadaCortePendiente"));
        Assert.Equal(["EST-006"], h.Log.StudentsConsulted);
        Assert.True(answer.UsedStudentData);
    }

    [Fact]
    public async Task Coordinator_tool_without_student_id_asks_for_it_without_api_calls()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus, new { estudianteId = "Laura" }))
            .EnqueueText(AssistantHarness.Final("Indica el ID."));

        var answer = await h.AskCoordinatorAsync("¿Cómo va Laura?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.True(h.ToolResult().TryGetProperty("error", out _));
        Assert.Empty(h.StudentDataCalls);
    }
}
