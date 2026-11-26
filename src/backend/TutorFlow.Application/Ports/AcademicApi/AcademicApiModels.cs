namespace TutorFlow.Application.Ports.AcademicApi;

// Port models mirroring material/mock-api/datos.json. Values are passed through as the API returns them:
// null means "no data", never 0 (API-10). Grades are decimal end to end (R-CAL-02, D-06): the adapter must
// read them from the JSON text as decimal, never through double.

/// <summary>Result of <c>GET /salud</c>.</summary>
/// <param name="Status">Field <c>estado</c> (e.g. <c>"ok"</c>).</param>
/// <param name="Period">Field <c>periodo</c>, the current academic period (e.g. <c>"2026-2"</c>). Source of the
/// period in data citations: it is never hard-coded (R-RES-02).</param>
public sealed record AcademicApiHealth(string Status, string Period);

/// <summary>An academic program (<c>GET /programas</c>).</summary>
/// <param name="Id">Field <c>id</c> (e.g. <c>"ING-SIS"</c>). Validate with <see cref="ProgramId.TryParse"/> before
/// using it in a route.</param>
/// <param name="Name">Field <c>nombre</c>.</param>
/// <param name="CoordinatorId">Field <c>coordinadorId</c>. Informational only: the coordinator-program relation
/// is taken exclusively from <see cref="CoordinatorRecord.ProgramIds"/> (API-08).</param>
public sealed record AcademicProgram(string Id, string Name, string? CoordinatorId);

/// <summary>A coordinator (<c>GET /coordinadores/{id}</c>).</summary>
/// <param name="Id">Field <c>id</c>.</param>
/// <param name="Name">Field <c>nombre</c>. Never shown to students (R-SEG-10, D-11).</param>
/// <param name="ProgramIds">Field <c>programas</c>: the single source of the programs in charge (API-08).</param>
public sealed record CoordinatorRecord(string Id, string Name, IReadOnlyList<string> ProgramIds);

/// <summary>Basic student data (<c>GET /estudiantes/{id}</c> and items of <c>GET /programas/{id}/estudiantes</c>).</summary>
/// <param name="Id">Field <c>id</c>.</param>
/// <param name="Name">Field <c>nombre</c>. Never written to the audit log (R-LOG-02).</param>
/// <param name="ProgramId">Field <c>programaId</c>.</param>
/// <param name="Semester">Field <c>semestre</c>; <see langword="null"/> if the API sends no value.</param>
public sealed record StudentRecord(string Id, string Name, string ProgramId, int? Semester);

/// <summary>Grades of one course by cut (<c>GET /estudiantes/{id}/notas</c>). The API sends no final grade.</summary>
/// <param name="StudentId">Field <c>estudianteId</c>.</param>
/// <param name="CourseCode">Field <c>codigo</c>, the join key with attendance (API-09). It may look like a student
/// id (<c>EST-103</c>, CL-08) and is never treated as one.</param>
/// <param name="CourseName">Field <c>asignatura</c>.</param>
/// <param name="Credits">Field <c>creditos</c>; <see langword="null"/> means no data (R-CAL-12).</param>
/// <param name="Cut1">Field <c>corte1</c> (30 %); <see langword="null"/> means not graded yet (R-CAL-05).</param>
/// <param name="Cut2">Field <c>corte2</c> (30 %); <see langword="null"/> means not graded yet (R-CAL-05).</param>
/// <param name="Cut3">Field <c>corte3</c> (40 %); <see langword="null"/> means not graded yet (R-CAL-05).</param>
public sealed record GradeRecord(
    string StudentId,
    string CourseCode,
    string CourseName,
    int? Credits,
    decimal? Cut1,
    decimal? Cut2,
    decimal? Cut3);

/// <summary>Attendance of one course (<c>GET /estudiantes/{id}/asistencia</c>).</summary>
/// <param name="StudentId">Field <c>estudianteId</c>.</param>
/// <param name="CourseCode">Field <c>codigo</c>, the join key with grades (API-09).</param>
/// <param name="CourseName">Field <c>asignatura</c>.</param>
/// <param name="ScheduledClasses">Field <c>clasesProgramadas</c>; <see langword="null"/> means no data (R-CAL-12).</param>
/// <param name="Absences">Field <c>inasistencias</c>; <see langword="null"/> means unknown, not 0 (R-ASI-03).</param>
public sealed record AttendanceRecord(
    string StudentId,
    string CourseCode,
    string CourseName,
    int? ScheduledClasses,
    int? Absences);
