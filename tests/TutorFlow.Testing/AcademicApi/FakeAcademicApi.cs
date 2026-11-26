using System.Collections.Concurrent;
using System.Text.Json;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Ports.AcademicApi;

namespace TutorFlow.Testing.AcademicApi;

/// <summary>
/// In-process double of the simulated academic API (<c>material/mock-api</c>), built from <c>datos.json</c>, with no
/// network. It follows the same semantics as <c>server.js</c>: unknown ids give <see langword="null"/>, lists may be
/// empty, <c>null</c> fields are preserved (API-10). Every call is recorded in <see cref="Calls"/> using the canonical
/// routes of <see cref="AcademicApiRoutes"/>, so tests can assert "zero calls" or the exact routes.
/// Call <see cref="Stop"/> to simulate the <c>api-simulada-detenida</c> precondition (escenarios/README §2.1).
/// </summary>
public sealed class FakeAcademicApi : IAcademicApi
{
    private readonly ConcurrentQueue<string> _calls = new();
    private readonly string _period;
    private readonly IReadOnlyList<AcademicProgram> _programs;
    private readonly IReadOnlyList<CoordinatorRecord> _coordinators;
    private readonly IReadOnlyList<StudentRecord> _students;
    private readonly IReadOnlyList<GradeRecord> _grades;
    private readonly IReadOnlyList<AttendanceRecord> _attendance;
    private volatile bool _stopped;

    private FakeAcademicApi(JsonElement root)
    {
        _period = root.GetProperty("periodo").GetString()!;
        _programs = Array(root, "programas", e => new AcademicProgram(Str(e, "id"), Str(e, "nombre"), OptStr(e, "coordinadorId")));
        _coordinators = Array(root, "coordinadores", e => new CoordinatorRecord(
            Str(e, "id"), Str(e, "nombre"),
            e.GetProperty("programas").EnumerateArray().Select(p => p.GetString()!).ToList()));
        _students = Array(root, "estudiantes", e => new StudentRecord(Str(e, "id"), Str(e, "nombre"), Str(e, "programaId"), OptInt(e, "semestre")));
        _grades = Array(root, "notas", e => new GradeRecord(
            Str(e, "estudianteId"), Str(e, "codigo"), Str(e, "asignatura"),
            OptInt(e, "creditos"), OptDecimal(e, "corte1"), OptDecimal(e, "corte2"), OptDecimal(e, "corte3")));
        _attendance = Array(root, "asistencia", e => new AttendanceRecord(
            Str(e, "estudianteId"), Str(e, "codigo"), Str(e, "asignatura"),
            OptInt(e, "clasesProgramadas"), OptInt(e, "inasistencias")));
    }

    /// <summary>Routes requested so far, in order, including the ones made while stopped.</summary>
    public IReadOnlyList<string> Calls => _calls.ToArray();

    /// <summary>Whether the double currently simulates a stopped API.</summary>
    public bool IsStopped => _stopped;

    /// <summary>Builds the double from <c>material/mock-api/datos.json</c>, located walking up from the test binaries.</summary>
    /// <returns>A running double.</returns>
    public static FakeAcademicApi FromDefaultData() => FromFile(FindDefaultDataFile());

    /// <summary>Builds the double from a <c>datos.json</c> file.</summary>
    /// <param name="path">Absolute path of the JSON file.</param>
    /// <returns>A running double.</returns>
    public static FakeAcademicApi FromFile(string path) => FromJson(File.ReadAllText(path));

    /// <summary>Builds the double from JSON text with the <c>datos.json</c> shape.</summary>
    /// <param name="json">JSON text.</param>
    /// <returns>A running double.</returns>
    public static FakeAcademicApi FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new FakeAcademicApi(doc.RootElement);
    }

    /// <summary>Builds a double that is already stopped (<c>api-simulada-detenida</c>).</summary>
    /// <returns>A stopped double.</returns>
    public static FakeAcademicApi CreateStopped()
    {
        var api = FromDefaultData();
        api.Stop();
        return api;
    }

    /// <summary>Makes every following call throw <see cref="DependencyUnavailableException"/>.</summary>
    public void Stop() => _stopped = true;

    /// <summary>Makes the double answer again.</summary>
    public void Start() => _stopped = false;

    /// <summary>Forgets the recorded calls.</summary>
    public void ClearCalls() => _calls.Clear();

    /// <inheritdoc />
    public Task<AcademicApiHealth> GetHealthAsync(CancellationToken cancellationToken) =>
        Respond(AcademicApiRoutes.Health, () => new AcademicApiHealth("ok", _period));

    /// <inheritdoc />
    public Task<IReadOnlyList<AcademicProgram>> GetProgramsAsync(CancellationToken cancellationToken) =>
        Respond(AcademicApiRoutes.Programs, () => _programs);

    /// <inheritdoc />
    public Task<IReadOnlyList<StudentRecord>?> GetProgramStudentsAsync(ProgramId programId, CancellationToken cancellationToken) =>
        Respond<IReadOnlyList<StudentRecord>?>(AcademicApiRoutes.ProgramStudents(programId), () =>
            _programs.Any(p => p.Id == programId.Value)
                ? _students.Where(s => s.ProgramId == programId.Value).ToList()
                : null);

    /// <inheritdoc />
    public Task<CoordinatorRecord?> GetCoordinatorAsync(CoordinatorId coordinatorId, CancellationToken cancellationToken) =>
        Respond(AcademicApiRoutes.Coordinator(coordinatorId), () =>
            _coordinators.FirstOrDefault(c => c.Id == coordinatorId.Value));

    /// <inheritdoc />
    public Task<StudentRecord?> GetStudentAsync(StudentId studentId, CancellationToken cancellationToken) =>
        Respond(AcademicApiRoutes.Student(studentId), () => _students.FirstOrDefault(s => s.Id == studentId.Value));

    /// <inheritdoc />
    public Task<IReadOnlyList<GradeRecord>?> GetGradesAsync(StudentId studentId, CancellationToken cancellationToken) =>
        Respond<IReadOnlyList<GradeRecord>?>(AcademicApiRoutes.StudentGrades(studentId), () =>
            _students.Any(s => s.Id == studentId.Value)
                ? _grades.Where(g => g.StudentId == studentId.Value).ToList()
                : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<AttendanceRecord>?> GetAttendanceAsync(StudentId studentId, CancellationToken cancellationToken) =>
        Respond<IReadOnlyList<AttendanceRecord>?>(AcademicApiRoutes.StudentAttendance(studentId), () =>
            _students.Any(s => s.Id == studentId.Value)
                ? _attendance.Where(a => a.StudentId == studentId.Value).ToList()
                : null);

    private Task<T> Respond<T>(string route, Func<T> answer)
    {
        _calls.Enqueue(route);
        if (_stopped)
        {
            throw new DependencyUnavailableException(ExternalDependency.AcademicApi,
                $"Simulated academic API is stopped ({route}).");
        }

        return Task.FromResult(answer());
    }

    private static string FindDefaultDataFile()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "material", "mock-api", "datos.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("material/mock-api/datos.json not found above " + AppContext.BaseDirectory);
    }

    private static List<T> Array<T>(JsonElement root, string name, Func<JsonElement, T> map) =>
        root.GetProperty(name).EnumerateArray().Select(map).ToList();

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private static string? OptStr(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetString() : null;

    private static int? OptInt(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetInt32() : null;

    private static decimal? OptDecimal(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetDecimal() : null;
}
