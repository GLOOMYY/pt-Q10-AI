using TutorFlow.Api.Endpoints;
using TutorFlow.Application.Audit;

namespace TutorFlow.Api.Middleware;

/// <summary>
/// Security middleware of the audit trail (R-LOG-01, R-LOG-09, D-30). For every request it sets the
/// <c>X-Correlation-Id</c> response header (reusing the request's value when it is reasonable). For
/// <c>/api/asistente/preguntar</c> it also starts the per-request audit context and, when the response is decided,
/// writes <b>exactly one</b> audit record with the status really returned: 200, 400, 403 or 503, and also the status of
/// an unhandled exception, which this middleware answers itself with an empty 500 that keeps the header.
/// </summary>
/// <remarks>
/// The reused correlation id is restricted: only 1 to 128 printable ASCII characters without spaces are accepted; any other
/// value is silently replaced by a generated id (see <see cref="CorrelationId.Resolve"/>), so a caller must read the id from
/// the response header. The restriction keeps the value safe in a header and in a JSON line.
/// The scoped services are taken from <c>HttpContext.RequestServices</c> for each request, never from the constructor.
/// Register it early (see <see cref="AuditTrailApplicationBuilderExtensions.UseAuditTrail"/>) so it wraps the endpoint
/// and everything that can fail around it.
/// </remarks>
public sealed partial class AuditTrailMiddleware
{
    /// <summary>Status recorded when the client closed the connection before the response was produced.</summary>
    public const int ClientClosedRequestStatus = 499;

    private readonly RequestDelegate _next;
    private readonly ILogger<AuditTrailMiddleware> _logger;

    /// <summary>Creates the middleware.</summary>
    /// <param name="next">The next delegate of the pipeline.</param>
    /// <param name="logger">Technical logger (never receives request data).</param>
    public AuditTrailMiddleware(RequestDelegate next, ILogger<AuditTrailMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Handles one request.</summary>
    /// <param name="httpContext">The request context.</param>
    /// <returns>A task that completes when the request, and its audit record, are done.</returns>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var correlationId = CorrelationId.Resolve(httpContext.Request.Headers[CorrelationId.HeaderName].FirstOrDefault());

        // OnStarting guarantees the header is on the response whatever the endpoint or an error handler does later.
        httpContext.Response.OnStarting(
            static state =>
            {
                var (response, id) = ((HttpResponse, string))state;
                response.Headers[CorrelationId.HeaderName] = id;
                return Task.CompletedTask;
            },
            (httpContext.Response, correlationId));

        var recorder = IsAuditedPath(httpContext.Request.Path)
            ? httpContext.RequestServices.GetRequiredService<AuditTrailRecorder>()
            : null;
        recorder?.Begin(correlationId);

        var status = 0;
        try
        {
            await _next(httpContext).ConfigureAwait(false);
            status = httpContext.Response.StatusCode;
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client left: nobody is waiting for a response.
            status = ClientClosedRequestStatus;
        }
        catch (Exception ex)
        {
            // Only the exception type is logged: its message can quote the request or the LLM provider's error
            // (R-LOG-02, R-LOG-05).
            LogUnhandled(ex.GetType().FullName);
            status = await RespondInternalErrorAsync(httpContext).ConfigureAwait(false);
        }

        if (recorder is not null)
        {
            await recorder.FinishAsync(status).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Answers an unhandled exception itself, so the response keeps <c>X-Correlation-Id</c> (R-LOG-09): rethrowing would let
    /// the host clear the headers. The body is empty: the SPEC defines no body for a 500 (DUDA-25). If the response had
    /// already started it cannot be changed, so the connection is aborted and the status already sent is recorded.
    /// </summary>
    private static Task<int> RespondInternalErrorAsync(HttpContext httpContext)
    {
        if (httpContext.Response.HasStarted)
        {
            httpContext.Abort();
            return Task.FromResult(httpContext.Response.StatusCode);
        }

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentLength = 0;
        return Task.FromResult(StatusCodes.Status500InternalServerError);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception of type {ExceptionType} while handling a request.")]
    private partial void LogUnhandled(string? exceptionType);

    private static bool IsAuditedPath(PathString path) =>
        path.HasValue
        && path.Value!.TrimEnd('/').Equals(AssistantEndpoint.Route, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Registration of <see cref="AuditTrailMiddleware"/> in the pipeline.</summary>
public static class AuditTrailApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the audit trail to the pipeline. <c>Program.cs</c> (plataforma) must call it <b>before</b> mapping the
    /// endpoints, as one of the first middlewares, together with <c>AddAudit()</c>, <c>AddAuditLog()</c> and an
    /// <c>IClock</c> registration.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IApplicationBuilder UseAuditTrail(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<AuditTrailMiddleware>();
    }
}
