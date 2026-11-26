using Microsoft.Extensions.Logging;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Ports.Clock;

namespace TutorFlow.Application.Audit;

/// <summary>
/// Orchestrates the audit record of one request, for the security middleware: it starts the
/// <see cref="IRequestAuditContext"/> and, at the end, builds the record with the real HTTP status and writes it
/// <b>exactly once</b> (R-LOG-01). The HTTP details (headers, path, status) stay in the middleware; the rules live here
/// so they can be tested without a web server.
/// </summary>
public sealed partial class AuditTrailRecorder
{
    private readonly IRequestAuditContext _context;
    private readonly IAuditLog _log;
    private readonly IClock _clock;
    private readonly ILogger<AuditTrailRecorder> _logger;
    private bool _started;
    private bool _written;

    /// <summary>Creates the recorder of a request (scoped).</summary>
    /// <param name="context">The per-request audit context.</param>
    /// <param name="log">The audit log port.</param>
    /// <param name="clock">The clock port.</param>
    /// <param name="logger">Technical logger (never receives request data).</param>
    public AuditTrailRecorder(
        IRequestAuditContext context,
        IAuditLog log,
        IClock clock,
        ILogger<AuditTrailRecorder> logger)
    {
        _context = context;
        _log = log;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Starts the context with the correlation id the request will use (R-LOG-09).</summary>
    /// <param name="correlationId">The id already resolved with <see cref="CorrelationId.Resolve"/>.</param>
    public void Begin(string correlationId)
    {
        _context.Start(correlationId, _clock.UtcNow);
        _started = true;
    }

    /// <summary>
    /// Builds the record with the status really returned and writes it, once. A second call does nothing. If the log
    /// cannot be written the failure is reported in the technical log and swallowed: the response of the user is already
    /// decided and must not change because of the audit disk.
    /// </summary>
    /// <param name="status">HTTP status actually returned.</param>
    /// <returns>A task that completes when the record has been handled.</returns>
    public async Task FinishAsync(int status)
    {
        if (!_started || _written)
        {
            return;
        }

        _written = true;
        try
        {
            var record = _context.BuildRecord(status, _clock.UtcNow);

            // The write must not be cut by a client that disconnects: the record is mandatory (R-LOG-01).
            await _log.WriteAsync(record, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogWriteFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The audit record could not be written.")]
    private partial void LogWriteFailed(Exception exception);
}
