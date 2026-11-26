namespace TutorFlow.Application.Ports.Audit;

/// <summary>Port to the audit log (R-LOG).</summary>
/// <remarks>
/// <b>Implemented by:</b> <c>seguridad</c>: the adapter in <c>TutorFlow.Infrastructure/Audit/</c> writes one JSON line
/// per request to stdout and appends it to <c>./var/auditoria/auditoria.jsonl</c> (R-LOG-07, D-30); the in-memory log
/// for tests lives in <c>tests/TutorFlow.Testing/Audit/</c> (R-LOG-09). The log is never exposed over HTTP.
/// </remarks>
public interface IAuditLog
{
    /// <summary>Writes the single audit record of a request (R-LOG-01).</summary>
    /// <param name="record">The record, with the fields of R-LOG-08.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the record has been written.</returns>
    Task WriteAsync(AuditRecord record, CancellationToken cancellationToken);
}
