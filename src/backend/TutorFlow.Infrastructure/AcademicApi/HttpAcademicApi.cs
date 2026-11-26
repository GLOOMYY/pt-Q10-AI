using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Ports.AcademicApi;

namespace TutorFlow.Infrastructure.AcademicApi;

/// <summary>
/// HTTP adapter of <see cref="IAcademicApi"/> over the simulated API. Identifiers arrive already validated and typed
/// (API-01); routes come from <see cref="AcademicApiRoutes"/> (escaped, no trailing slash, API-05). Both kinds of
/// 404 give <see langword="null"/> but are logged differently (API-07). Network failures, timeouts, 5xx and
/// unreadable payloads throw <see cref="DependencyUnavailableException"/> (API-02, R-QA-04). The adapter does not
/// write the audit trail: the caller does.
/// </summary>
public sealed partial class HttpAcademicApi : IAcademicApi
{
    private const string NotFoundMessage = "No encontrado";

    private readonly HttpClient _http;
    private readonly ILogger<HttpAcademicApi> _logger;

    /// <summary>Creates the adapter.</summary>
    /// <param name="http">Client whose <c>BaseAddress</c> is the API root and whose <c>Timeout</c> is 5 s by default.</param>
    /// <param name="logger">Technical logger.</param>
    public HttpAcademicApi(HttpClient http, ILogger<HttpAcademicApi> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AcademicApiHealth> GetHealthAsync(CancellationToken cancellationToken)
    {
        var doc = await GetRequiredAsync(AcademicApiRoutes.Health, cancellationToken);
        return Parse(AcademicApiRoutes.Health, doc, root =>
            new AcademicApiHealth(Str(root, "estado"), Str(root, "periodo")));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AcademicProgram>> GetProgramsAsync(CancellationToken cancellationToken)
    {
        var doc = await GetRequiredAsync(AcademicApiRoutes.Programs, cancellationToken);
        return Parse(AcademicApiRoutes.Programs, doc, root =>
            List(root, e => new AcademicProgram(Str(e, "id"), Str(e, "nombre"), OptStr(e, "coordinadorId"))));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentRecord>?> GetProgramStudentsAsync(ProgramId programId, CancellationToken cancellationToken)
    {
        var route = AcademicApiRoutes.ProgramStudents(programId);
        var doc = await GetOptionalAsync(route, cancellationToken);
        return doc is null ? null : Parse(route, doc, root => List(root, ToStudent));
    }

    /// <inheritdoc />
    public async Task<CoordinatorRecord?> GetCoordinatorAsync(CoordinatorId coordinatorId, CancellationToken cancellationToken)
    {
        var route = AcademicApiRoutes.Coordinator(coordinatorId);
        var doc = await GetOptionalAsync(route, cancellationToken);
        return doc is null
            ? null
            : Parse(route, doc, root => new CoordinatorRecord(
                Str(root, "id"),
                Str(root, "nombre"),
                root.GetProperty("programas").EnumerateArray()
                    .Select(p => p.GetString() ?? throw new JsonException("null program id")).ToList()));
    }

    /// <inheritdoc />
    public async Task<StudentRecord?> GetStudentAsync(StudentId studentId, CancellationToken cancellationToken)
    {
        var route = AcademicApiRoutes.Student(studentId);
        var doc = await GetOptionalAsync(route, cancellationToken);
        return doc is null ? null : Parse(route, doc, ToStudent);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GradeRecord>?> GetGradesAsync(StudentId studentId, CancellationToken cancellationToken)
    {
        var route = AcademicApiRoutes.StudentGrades(studentId);
        var doc = await GetOptionalAsync(route, cancellationToken);
        return doc is null
            ? null
            : Parse(route, doc, root => List(root, e => new GradeRecord(
                Str(e, "estudianteId"), Str(e, "codigo"), Str(e, "asignatura"),
                OptInt(e, "creditos"), OptDecimal(e, "corte1"), OptDecimal(e, "corte2"), OptDecimal(e, "corte3"))));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>?> GetAttendanceAsync(StudentId studentId, CancellationToken cancellationToken)
    {
        var route = AcademicApiRoutes.StudentAttendance(studentId);
        var doc = await GetOptionalAsync(route, cancellationToken);
        return doc is null
            ? null
            : Parse(route, doc, root => List(root, e => new AttendanceRecord(
                Str(e, "estudianteId"), Str(e, "codigo"), Str(e, "asignatura"),
                OptInt(e, "clasesProgramadas"), OptInt(e, "inasistencias"))));
    }

    private static StudentRecord ToStudent(JsonElement e) =>
        new(Str(e, "id"), Str(e, "nombre"), Str(e, "programaId"), OptInt(e, "semestre"));

    private static string Str(JsonElement e, string name) =>
        e.GetProperty(name).GetString() ?? throw new JsonException($"'{name}' is null");

    private static string? OptStr(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetString() : null;

    // null (or absent) is "no data", never 0 (API-10).
    private static int? OptInt(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetInt32() : null;

    // Read from the JSON text as decimal, never through double (R-CAL-02).
    private static decimal? OptDecimal(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetDecimal() : null;

    private static List<T> List<T>(JsonElement root, Func<JsonElement, T> map) =>
        root.EnumerateArray().Select(map).ToList();

    private T Parse<T>(string route, JsonDocument doc, Func<JsonElement, T> map)
    {
        using (doc)
        {
            try
            {
                return map(doc.RootElement);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException
                                           or FormatException or OverflowException)
            {
                LogUnreadable(route, ex);
                throw new DependencyUnavailableException(ExternalDependency.AcademicApi,
                    $"Academic API returned an unreadable payload for {route}.", ex);
            }
        }
    }

    /// <summary>GET where a 404 means "the resource does not exist" (returns null).</summary>
    private async Task<JsonDocument?> GetOptionalAsync(string route, CancellationToken ct)
    {
        var (status, body) = await SendAsync(route, ct);
        if (status == HttpStatusCode.NotFound)
        {
            // API-07: both 404s are "not found" for the user, but only "No encontrado" is a real missing resource.
            if (IsResourceNotFound(body))
            {
                LogResourceNotFound(route);
            }
            else
            {
                LogRouteNotFound(route);
            }

            return null;
        }

        return ReadBody(route, status, body);
    }

    /// <summary>GET of a collection or the health check: a 404 is a contract failure, not "no data".</summary>
    private async Task<JsonDocument> GetRequiredAsync(string route, CancellationToken ct)
    {
        var (status, body) = await SendAsync(route, ct);
        if (status == HttpStatusCode.NotFound)
        {
            LogRouteNotFound(route);
            throw new DependencyUnavailableException(ExternalDependency.AcademicApi,
                $"Academic API answered 404 for {route}.");
        }

        return ReadBody(route, status, body);
    }

    private JsonDocument ReadBody(string route, HttpStatusCode status, string body)
    {
        if (status != HttpStatusCode.OK)
        {
            LogUnexpectedStatus(route, (int)status);
            throw new DependencyUnavailableException(ExternalDependency.AcademicApi,
                $"Academic API answered {(int)status} for {route}.");
        }

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            LogUnreadable(route, ex);
            throw new DependencyUnavailableException(ExternalDependency.AcademicApi,
                $"Academic API returned invalid JSON for {route}.", ex);
        }
    }

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(string route, CancellationToken ct)
    {
        try
        {
            // Relative without leading slash so the BaseAddress path (if any) is preserved; no trailing slash (API-05).
            using var response = await _http.GetAsync(route.TrimStart('/'), ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return (response.StatusCode, body);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            LogTimeout(route, ex);
            throw new DependencyUnavailableException(ExternalDependency.AcademicApi,
                $"Academic API timed out for {route}.", ex);
        }
        catch (HttpRequestException ex)
        {
            LogNetworkFailure(route, ex);
            throw new DependencyUnavailableException(ExternalDependency.AcademicApi,
                $"Academic API is unreachable for {route}.", ex);
        }
    }

    private static bool IsResourceNotFound(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("error", out var error)
                   && error.ValueKind == JsonValueKind.String
                   && string.Equals(error.GetString(), NotFoundMessage, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Academic API: resource not found (404 'No encontrado') for {Route}")]
    private partial void LogResourceNotFound(string route);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Academic API: route does not exist (404 'Ruta no existe') for {Route}; this is a bug in the request, not a missing resource")]
    private partial void LogRouteNotFound(string route);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Academic API: unexpected status {Status} for {Route}")]
    private partial void LogUnexpectedStatus(string route, int status);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Academic API: timeout for {Route}")]
    private partial void LogTimeout(string route, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Academic API: network failure for {Route}")]
    private partial void LogNetworkFailure(string route, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Academic API: unreadable payload for {Route}")]
    private partial void LogUnreadable(string route, Exception exception);
}
