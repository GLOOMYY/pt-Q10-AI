using TutorFlow.Application.Authorization;
using TutorFlow.Application.Ports.AcademicApi;

namespace TutorFlow.Application.Academic;

/// <summary>
/// Per-request gateway to the simulated academic API (scoped: one instance per HTTP request). Every component of the
/// request (identity check, deterministic barrier, tools) reads the API through it, so a route is requested at most
/// once per request and every route is recorded in <c>recursosConsultados</c> right before the call (R-LOG-04, R-LOG-08).
/// </summary>
/// <remarks>
/// <para><b>Directory methods</b> (<see cref="GetHealthAsync"/>, <see cref="GetProgramsAsync"/>,
/// <see cref="GetProgramStudentsAsync"/>, <see cref="GetCoordinatorAsync"/>) never record a consulted student: they are
/// used to authorize, to validate the identity, to build the barrier list or to list a program (R-SEG-03, R-SEG-04,
/// R-SEG-14, R-LOG-06, D-24).</para>
/// <para><b>Student data methods</b> (<see cref="GetGradesAsync"/>, <see cref="GetAttendanceAsync"/>) only accept an
/// <see cref="AuthorizedStudent"/>, which only the deterministic authorizer can build (R-SEG-01..03), and record the
/// student in <c>estudiantesConsultados</c> (R-LOG-06).</para>
/// <para>All methods throw <see cref="Ports.DependencyUnavailableException"/> when the API is not available (503,
/// API-02). Failed calls are not cached.</para>
/// </remarks>
public interface IAcademicDirectory
{
    /// <summary><c>GET /salud</c>: current academic period, used in data citations (R-RES-02). Never hard-coded.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The period, e.g. <c>2026-2</c>.</returns>
    Task<string> GetPeriodAsync(CancellationToken cancellationToken);

    /// <summary><c>GET /salud</c>.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The API status and period.</returns>
    Task<AcademicApiHealth> GetHealthAsync(CancellationToken cancellationToken);

    /// <summary><c>GET /programas</c>.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All programs.</returns>
    Task<IReadOnlyList<AcademicProgram>> GetProgramsAsync(CancellationToken cancellationToken);

    /// <summary><c>GET /programas/{id}/estudiantes</c>. Does not count as consulting the listed students (D-24).</summary>
    /// <param name="programId">Validated program id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The roster, or <see langword="null"/> if the program does not exist.</returns>
    Task<IReadOnlyList<StudentRecord>?> GetProgramStudentsAsync(ProgramId programId, CancellationToken cancellationToken);

    /// <summary>
    /// The rosters of every program returned by <c>GET /programas</c> whose id is well formed (API-01), keyed by program
    /// id. Programs whose roster does not exist are left out.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rosters, in the order of <c>GET /programas</c>.</returns>
    Task<IReadOnlyList<ProgramRoster>> GetAllRostersAsync(CancellationToken cancellationToken);

    /// <summary><c>GET /coordinadores/{id}</c> (API-08: single source of the programs in charge).</summary>
    /// <param name="coordinatorId">Validated coordinator id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The coordinator, or <see langword="null"/> if it does not exist.</returns>
    Task<CoordinatorRecord?> GetCoordinatorAsync(CoordinatorId coordinatorId, CancellationToken cancellationToken);

    /// <summary><c>GET /estudiantes/{id}/notas</c> of an authorized student; records the student (R-LOG-06).</summary>
    /// <param name="student">The authorized student.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The grade rows, or <see langword="null"/> if the API has none for the student.</returns>
    Task<IReadOnlyList<GradeRecord>?> GetGradesAsync(AuthorizedStudent student, CancellationToken cancellationToken);

    /// <summary><c>GET /estudiantes/{id}/asistencia</c> of an authorized student; records the student (R-LOG-06).</summary>
    /// <param name="student">The authorized student.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The attendance rows, or <see langword="null"/> if the API has none for the student.</returns>
    Task<IReadOnlyList<AttendanceRecord>?> GetAttendanceAsync(AuthorizedStudent student, CancellationToken cancellationToken);
}

/// <summary>A program with its students.</summary>
/// <param name="ProgramId">The program id.</param>
/// <param name="Students">Its students as listed by <c>GET /programas/{id}/estudiantes</c>.</param>
public sealed record ProgramRoster(ProgramId ProgramId, IReadOnlyList<StudentRecord> Students);
