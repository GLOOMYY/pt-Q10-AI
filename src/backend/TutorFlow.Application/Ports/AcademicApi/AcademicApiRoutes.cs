namespace TutorFlow.Application.Ports.AcademicApi;

/// <summary>
/// Canonical routes of the simulated academic API (<c>material/README.md</c>). Single source for the adapter,
/// which builds its URLs with them, and for the audit trail (<c>recursosConsultados</c>, R-LOG-08).
/// </summary>
/// <remarks>
/// Routes never end with a slash (API-05) and every identifier is escaped with
/// <see cref="Uri.EscapeDataString(string)"/> on top of its strict validation (API-01).
/// </remarks>
public static class AcademicApiRoutes
{
    /// <summary><c>GET /salud</c>: status and current academic period.</summary>
    public const string Health = "/salud";

    /// <summary><c>GET /programas</c>: academic programs.</summary>
    public const string Programs = "/programas";

    /// <summary><c>GET /programas/{id}/estudiantes</c>.</summary>
    /// <param name="programId">Validated program identifier.</param>
    /// <returns>The relative route.</returns>
    public static string ProgramStudents(ProgramId programId)
    {
        ArgumentNullException.ThrowIfNull(programId);
        return $"/programas/{Uri.EscapeDataString(programId.Value)}/estudiantes";
    }

    /// <summary><c>GET /coordinadores/{id}</c>.</summary>
    /// <param name="coordinatorId">Validated coordinator identifier.</param>
    /// <returns>The relative route.</returns>
    public static string Coordinator(CoordinatorId coordinatorId)
    {
        ArgumentNullException.ThrowIfNull(coordinatorId);
        return $"/coordinadores/{Uri.EscapeDataString(coordinatorId.Value)}";
    }

    /// <summary><c>GET /estudiantes/{id}</c>.</summary>
    /// <param name="studentId">Validated student identifier.</param>
    /// <returns>The relative route.</returns>
    public static string Student(StudentId studentId)
    {
        ArgumentNullException.ThrowIfNull(studentId);
        return $"/estudiantes/{Uri.EscapeDataString(studentId.Value)}";
    }

    /// <summary><c>GET /estudiantes/{id}/notas</c>.</summary>
    /// <param name="studentId">Validated student identifier.</param>
    /// <returns>The relative route.</returns>
    public static string StudentGrades(StudentId studentId) => $"{Student(studentId)}/notas";

    /// <summary><c>GET /estudiantes/{id}/asistencia</c>.</summary>
    /// <param name="studentId">Validated student identifier.</param>
    /// <returns>The relative route.</returns>
    public static string StudentAttendance(StudentId studentId) => $"{Student(studentId)}/asistencia";
}
