using TutorFlow.Application.Authorization;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Academic;

/// <summary>
/// Scoped implementation of <see cref="IAcademicDirectory"/>: caches the successful answers of the request and records
/// each route in the audit context before calling the port, even if the call then fails (R-LOG-04). Not thread-safe:
/// the calls of one request are sequential (tools run one after another).
/// </summary>
public sealed class AcademicDirectory : IAcademicDirectory
{
    private readonly IAcademicApi _api;
    private readonly IRequestAuditContext _audit;
    private readonly Dictionary<string, object?> _cache = new(StringComparer.Ordinal);

    /// <summary>Creates the directory.</summary>
    /// <param name="api">The academic API port.</param>
    /// <param name="audit">The audit context of the request.</param>
    public AcademicDirectory(IAcademicApi api, IRequestAuditContext audit)
    {
        _api = api;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<string> GetPeriodAsync(CancellationToken cancellationToken) =>
        (await GetHealthAsync(cancellationToken).ConfigureAwait(false)).Period;

    /// <inheritdoc />
    public Task<AcademicApiHealth> GetHealthAsync(CancellationToken cancellationToken) =>
        GetOrAddAsync(AcademicApiRoutes.Health, () => _api.GetHealthAsync(cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AcademicProgram>> GetProgramsAsync(CancellationToken cancellationToken) =>
        GetOrAddAsync(AcademicApiRoutes.Programs, () => _api.GetProgramsAsync(cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<StudentRecord>?> GetProgramStudentsAsync(ProgramId programId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(programId);
        return GetOrAddAsync(
            AcademicApiRoutes.ProgramStudents(programId),
            () => _api.GetProgramStudentsAsync(programId, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProgramRoster>> GetAllRostersAsync(CancellationToken cancellationToken)
    {
        var programs = await GetProgramsAsync(cancellationToken).ConfigureAwait(false);
        var rosters = new List<ProgramRoster>();
        foreach (var program in programs)
        {
            // API-01: even ids returned by the API are validated before being put in a route.
            if (!ProgramId.TryParse(program.Id, out var programId) || rosters.Any(r => r.ProgramId == programId))
            {
                continue;
            }

            var students = await GetProgramStudentsAsync(programId, cancellationToken).ConfigureAwait(false);
            if (students is not null)
            {
                rosters.Add(new ProgramRoster(programId, students));
            }
        }

        return rosters;
    }

    /// <inheritdoc />
    public Task<CoordinatorRecord?> GetCoordinatorAsync(CoordinatorId coordinatorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(coordinatorId);
        return GetOrAddAsync(
            AcademicApiRoutes.Coordinator(coordinatorId),
            () => _api.GetCoordinatorAsync(coordinatorId, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GradeRecord>?> GetGradesAsync(AuthorizedStudent student, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(student);

        // R-LOG-06: /notas counts as consulting the student; recorded before the call (R-LOG-04).
        _audit.RecordStudentConsulted(student.Id);
        return GetOrAddAsync(
            AcademicApiRoutes.StudentGrades(student.Id),
            () => _api.GetGradesAsync(student.Id, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AttendanceRecord>?> GetAttendanceAsync(AuthorizedStudent student, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(student);
        _audit.RecordStudentConsulted(student.Id);
        return GetOrAddAsync(
            AcademicApiRoutes.StudentAttendance(student.Id),
            () => _api.GetAttendanceAsync(student.Id, cancellationToken),
            cancellationToken);
    }

    private async Task<T> GetOrAddAsync<T>(string route, Func<Task<T>> call, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_cache.TryGetValue(route, out var cached))
        {
            return (T)cached!;
        }

        // R-LOG-08: the route is recorded before the call, so a failed call still appears in the log.
        _audit.RecordResource(route);
        var value = await call().ConfigureAwait(false);
        _cache[route] = value;
        return value;
    }
}
