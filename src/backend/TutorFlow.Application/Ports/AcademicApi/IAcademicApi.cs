namespace TutorFlow.Application.Ports.AcademicApi;

/// <summary>
/// Port to the simulated academic API (<c>material/mock-api</c>). The API does not check permissions (API-04):
/// callers must authorize before invoking any method (R-SEG-01..03).
/// </summary>
/// <remarks>
/// <para><b>Implemented by:</b> <c>datos</c>, in <c>TutorFlow.Infrastructure/AcademicApi/</c> (HTTP client with a 5 s
/// timeout, R-QA-04) and its in-process test double in <c>tests/TutorFlow.Testing/AcademicApi/</c>, built from
/// <c>material/mock-api/datos.json</c>.</para>
/// <para><b>Outcomes of every method:</b></para>
/// <list type="bullet">
/// <item><description>Data found: the value (lists may be empty).</description></item>
/// <item><description>Not found (<c>404 "No encontrado"</c> or <c>404 "Ruta no existe"</c>, API-07): <see langword="null"/>.
/// The adapter distinguishes both 404 kinds in its technical log only.</description></item>
/// <item><description>Not available (connection error, timeout, 5xx, unreadable payload): throws
/// <see cref="DependencyUnavailableException"/> with <see cref="ExternalDependency.AcademicApi"/>, which becomes a
/// 503 (API-02, CL-13).</description></item>
/// </list>
/// <para>Identifiers are typed and already validated (API-01); routes come from <see cref="AcademicApiRoutes"/>.</para>
/// </remarks>
public interface IAcademicApi
{
    /// <summary><c>GET /salud</c>: liveness and current academic period (R-RES-02).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The API status and period.</returns>
    Task<AcademicApiHealth> GetHealthAsync(CancellationToken cancellationToken);

    /// <summary><c>GET /programas</c>.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All academic programs.</returns>
    Task<IReadOnlyList<AcademicProgram>> GetProgramsAsync(CancellationToken cancellationToken);

    /// <summary><c>GET /programas/{id}/estudiantes</c>.</summary>
    /// <param name="programId">Validated program identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The students of the program, or <see langword="null"/> if the program does not exist.</returns>
    Task<IReadOnlyList<StudentRecord>?> GetProgramStudentsAsync(ProgramId programId, CancellationToken cancellationToken);

    /// <summary><c>GET /coordinadores/{id}</c>: identity check and programs in charge (R-SEG-03/04, API-08).</summary>
    /// <param name="coordinatorId">Validated coordinator identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The coordinator, or <see langword="null"/> if it does not exist.</returns>
    Task<CoordinatorRecord?> GetCoordinatorAsync(CoordinatorId coordinatorId, CancellationToken cancellationToken);

    /// <summary><c>GET /estudiantes/{id}</c>. Never used to authorize or to check identity (R-SEG-03/04).</summary>
    /// <param name="studentId">Validated and already authorized student identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The student, or <see langword="null"/> if it does not exist.</returns>
    Task<StudentRecord?> GetStudentAsync(StudentId studentId, CancellationToken cancellationToken);

    /// <summary><c>GET /estudiantes/{id}/notas</c>.</summary>
    /// <param name="studentId">Validated and already authorized student identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The grades by cut, or <see langword="null"/> if the student does not exist.</returns>
    Task<IReadOnlyList<GradeRecord>?> GetGradesAsync(StudentId studentId, CancellationToken cancellationToken);

    /// <summary><c>GET /estudiantes/{id}/asistencia</c>.</summary>
    /// <param name="studentId">Validated and already authorized student identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The attendance rows, or <see langword="null"/> if the student does not exist.</returns>
    Task<IReadOnlyList<AttendanceRecord>?> GetAttendanceAsync(StudentId studentId, CancellationToken cancellationToken);
}
