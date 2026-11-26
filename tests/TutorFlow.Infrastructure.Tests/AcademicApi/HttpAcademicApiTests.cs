using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Infrastructure.AcademicApi;
using Xunit;

namespace TutorFlow.Infrastructure.Tests.AcademicApi;

public sealed class HttpAcademicApiTests
{
    private const string NotFoundBody = "{\"error\":\"No encontrado\"}";
    private const string NoRouteBody = "{\"error\":\"Ruta no existe\"}";

    private static StudentId Student(string raw) => StudentId.TryParse(raw, out var id) ? id : throw new InvalidOperationException(raw);

    private static (HttpAcademicApi Api, StubHandler Handler, CollectingLogger Log) Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond, TimeSpan? timeout = null)
    {
        var handler = new StubHandler(respond);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://mock-api:4000/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(5),
        };
        var log = new CollectingLogger();
        return (new HttpAcademicApi(client, log), handler, log);
    }

    private static Task<HttpResponseMessage> Json(HttpStatusCode status, string body) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    [Fact]
    public async Task Health_returns_the_period_from_the_api()
    {
        var (api, handler, _) = Create((_, _) => Json(HttpStatusCode.OK, "{\"estado\":\"ok\",\"periodo\":\"2027-1\"}"));

        var health = await api.GetHealthAsync(CancellationToken.None);

        Assert.Equal(new AcademicApiHealth("ok", "2027-1"), health);
        Assert.Equal(["/salud"], handler.Paths);
    }

    [Fact]
    public async Task Programs_are_read()
    {
        var (api, handler, _) = Create((_, _) => Json(HttpStatusCode.OK,
            "[{\"id\":\"ING-SIS\",\"nombre\":\"Ingeniería de Sistemas\",\"coordinadorId\":\"COORD-01\"}]"));

        var programs = await api.GetProgramsAsync(CancellationToken.None);

        Assert.Equal([new AcademicProgram("ING-SIS", "Ingeniería de Sistemas", "COORD-01")], programs);
        Assert.Equal(["/programas"], handler.Paths);
    }

    [Theory]
    [InlineData("est-001", "/estudiantes/EST-001")]
    [InlineData("  Est-002 ", "/estudiantes/EST-002")]
    public async Task Student_route_is_normalized_and_has_no_trailing_slash(string raw, string expectedPath)
    {
        var (api, handler, _) = Create((_, _) => Json(HttpStatusCode.OK,
            "{\"id\":\"EST-001\",\"nombre\":\"Laura\",\"programaId\":\"ING-SIS\",\"semestre\":3}"));

        var student = await api.GetStudentAsync(Student(raw), CancellationToken.None);

        Assert.NotNull(student);
        Assert.Equal([expectedPath], handler.Paths);
        Assert.Equal("GET", handler.Methods.Single());
    }

    [Theory]
    [InlineData("EST-00%")]
    [InlineData("EST-001%2F")]
    [InlineData("EST 001")]
    [InlineData("EST-001/notas")]
    [InlineData("../EST-001")]
    public void Ids_with_percent_spaces_or_slashes_cannot_be_built_so_no_call_is_possible(string raw)
    {
        Assert.False(StudentId.TryParse(raw, out _));
    }

    [Fact]
    public async Task Resource_not_found_returns_null_and_logs_information()
    {
        var (api, _, log) = Create((_, _) => Json(HttpStatusCode.NotFound, NotFoundBody));

        var student = await api.GetStudentAsync(Student("EST-999"), CancellationToken.None);

        Assert.Null(student);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("No encontrado", entry.Message);
    }

    [Fact]
    public async Task Route_does_not_exist_returns_null_and_logs_warning()
    {
        var (api, _, log) = Create((_, _) => Json(HttpStatusCode.NotFound, NoRouteBody));

        var grades = await api.GetGradesAsync(Student("EST-001"), CancellationToken.None);

        Assert.Null(grades);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("Ruta no existe", entry.Message);
    }

    [Fact]
    public async Task Not_found_for_program_and_coordinator_and_attendance_returns_null()
    {
        var (api, _, _) = Create((_, _) => Json(HttpStatusCode.NotFound, NotFoundBody));

        Assert.True(ProgramId.TryParse("ZZZ", out var program));
        Assert.True(CoordinatorId.TryParse("COORD-99", out var coordinator));
        Assert.Null(await api.GetProgramStudentsAsync(program, CancellationToken.None));
        Assert.Null(await api.GetCoordinatorAsync(coordinator, CancellationToken.None));
        Assert.Null(await api.GetAttendanceAsync(Student("EST-999"), CancellationToken.None));
    }

    [Fact]
    public async Task Null_fields_are_preserved_and_grades_are_exact_decimals()
    {
        var (api, handler, _) = Create((_, _) => Json(HttpStatusCode.OK,
            "[{\"estudianteId\":\"EST-001\",\"codigo\":\"CAL-201\",\"asignatura\":\"Cálculo\",\"creditos\":4,\"corte1\":3.5,\"corte2\":null,\"corte3\":2.85}," +
            "{\"estudianteId\":\"EST-001\",\"codigo\":\"EST-103\",\"asignatura\":\"X\",\"creditos\":null,\"corte1\":null,\"corte2\":null,\"corte3\":null}]"));

        var grades = await api.GetGradesAsync(Student("EST-001"), CancellationToken.None);

        Assert.NotNull(grades);
        Assert.Equal(new GradeRecord("EST-001", "CAL-201", "Cálculo", 4, 3.5m, null, 2.85m), grades[0]);
        Assert.Equal(new GradeRecord("EST-001", "EST-103", "X", null, null, null, null), grades[1]);
        Assert.Equal(["/estudiantes/EST-001/notas"], handler.Paths);
    }

    [Fact]
    public async Task Attendance_keeps_null_absences_as_unknown()
    {
        var (api, handler, _) = Create((_, _) => Json(HttpStatusCode.OK,
            "[{\"estudianteId\":\"EST-001\",\"codigo\":\"CAL-201\",\"asignatura\":\"C\",\"clasesProgramadas\":64,\"inasistencias\":null}]"));

        var rows = await api.GetAttendanceAsync(Student("EST-001"), CancellationToken.None);

        var row = Assert.Single(rows!);
        Assert.Equal(64, row.ScheduledClasses);
        Assert.Null(row.Absences);
        Assert.Equal(["/estudiantes/EST-001/asistencia"], handler.Paths);
    }

    [Fact]
    public async Task Coordinator_programs_come_from_the_coordinator_endpoint()
    {
        var (api, handler, _) = Create((_, _) => Json(HttpStatusCode.OK,
            "{\"id\":\"COORD-01\",\"nombre\":\"Marcela\",\"programas\":[\"ING-SIS\"]}"));

        Assert.True(CoordinatorId.TryParse(" coord-01", out var id));
        var coordinator = await api.GetCoordinatorAsync(id, CancellationToken.None);

        Assert.Equal(["ING-SIS"], coordinator!.ProgramIds);
        Assert.Equal(["/coordinadores/COORD-01"], handler.Paths);
    }

    [Fact]
    public async Task Program_students_route_is_exact_and_empty_list_is_not_null()
    {
        var (api, handler, _) = Create((_, _) => Json(HttpStatusCode.OK, "[]"));

        Assert.True(ProgramId.TryParse("ing-sis", out var program));
        var students = await api.GetProgramStudentsAsync(program, CancellationToken.None);

        Assert.NotNull(students);
        Assert.Empty(students);
        Assert.Equal(["/programas/ING-SIS/estudiantes"], handler.Paths);
    }

    [Fact]
    public async Task Timeout_throws_dependency_unavailable()
    {
        var (api, _, log) = Create(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, TimeSpan.FromMilliseconds(50));

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => api.GetStudentAsync(Student("EST-001"), CancellationToken.None));

        Assert.Equal(ExternalDependency.AcademicApi, ex.Dependency);
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_translated()
    {
        using var cts = new CancellationTokenSource();
        var (api, _, _) = Create(async (_, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.GetStudentAsync(Student("EST-001"), cts.Token));
    }

    [Fact]
    public async Task Connection_failure_throws_dependency_unavailable()
    {
        var (api, _, _) = Create((_, _) => throw new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(() => api.GetHealthAsync(CancellationToken.None));

        Assert.Equal(ExternalDependency.AcademicApi, ex.Dependency);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    public async Task Unexpected_status_throws_dependency_unavailable(HttpStatusCode status)
    {
        var (api, _, _) = Create((_, _) => Json(status, "{\"error\":\"x\"}"));

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => api.GetStudentAsync(Student("EST-001"), CancellationToken.None));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"id\":\"EST-001\"}")]
    public async Task Unreadable_payload_throws_dependency_unavailable(string body)
    {
        var (api, _, _) = Create((_, _) => Json(HttpStatusCode.OK, body));

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => api.GetStudentAsync(Student("EST-001"), CancellationToken.None));
    }

    [Fact]
    public async Task Collection_404_is_a_contract_failure_not_empty_data()
    {
        var (api, _, _) = Create((_, _) => Json(HttpStatusCode.NotFound, NoRouteBody));

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => api.GetProgramsAsync(CancellationToken.None));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private readonly List<string> _paths = [];
        private readonly List<string> _methods = [];

        public IReadOnlyList<string> Paths => _paths;

        public IReadOnlyList<string> Methods => _methods;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _paths.Add(request.RequestUri!.AbsolutePath);
            _methods.Add(request.Method.Method);
            return respond(request, cancellationToken);
        }
    }

    private sealed class CollectingLogger : ILogger<HttpAcademicApi>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
