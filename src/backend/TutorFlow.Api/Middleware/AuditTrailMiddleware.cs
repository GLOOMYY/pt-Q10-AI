using TutorFlow.Api.Endpoints;
using TutorFlow.Application.Audit;

namespace TutorFlow.Api.Middleware;

/// <summary>
/// Security middleware of the audit trail (R-LOG-01, R-LOG-09, D-30). For every request it sets the
/// <c>X-Correlation-Id</c> response header (reusing the request's value when it is reasonable). For
/// <c>/api/asistente/preguntar</c> it also starts the per-request audit context and, when the response is decided,
/// writes <b>exactly one</b> audit record with the status really returned: 200, 400, 403 or 503, and also the status of
/// an unhandled exception.
/// </summary>
/// <remarks>
/// The scoped services are taken from <c>HttpContext.RequestServices</c> for each request, never from the constructor.
/// Register it early (see <see cref="AuditTrailApplicationBuilderExtensions.UseAuditTrail"/>) so it wraps the endpoint
/// and everything that can fail around it.
/// </remarks>
public sealed class AuditTrailMiddleware
{
    /// <summary>Status recorded when the client closed the connection before the response was produced.</summary>
    public const int ClientClosedRequestStatus = 499;

    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware.</summary>
    /// <param name="next">The next delegate of the pipeline.</param>
    public AuditTrailMiddleware(RequestDelegate next)
    {
        _next = next;
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

        if (!IsAuditedPath(httpContext.Request.Path))
        {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        var recorder = httpContext.RequestServices.GetRequiredService<AuditTrailRecorder>();
        recorder.Begin(correlationId);

        try
        {
            await _next(httpContext).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The status really returned for an unhandled exception is the 500 of the host; a client that left gets 499.
            var status = ex is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested
                ? ClientClosedRequestStatus
                : StatusCodes.Status500InternalServerError;
            await recorder.FinishAsync(status).ConfigureAwait(false);
            throw;
        }

        await recorder.FinishAsync(httpContext.Response.StatusCode).ConfigureAwait(false);
    }

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
