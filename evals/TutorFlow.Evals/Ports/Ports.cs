using System.Text.Json;

namespace TutorFlow.Evals.Ports;

/// <summary>
/// Source of the audit records of the system under test (README section 5.2): in <c>evals/</c> it is the file
/// <c>./var/auditoria/auditoria.jsonl</c> that the backend writes in the mounted volume (R-LOG-07).
/// </summary>
public interface IAuditSource
{
    /// <summary>Finds the records whose <c>correlationId</c> is the given one.</summary>
    /// <param name="correlationId">The <c>X-Correlation-Id</c> the evals sent.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The matching records (exactly one is expected).</returns>
    Task<IReadOnlyList<JsonElement>> FindAsync(string correlationId, CancellationToken cancellationToken);
}

/// <summary>
/// Switch of the simulated API for the <c>api-simulada-detenida</c> precondition (README section 2.1): in <c>evals/</c> it
/// stops the <c>mock-api</c> container before the request and starts it again after.
/// </summary>
public interface IMockApiSwitch
{
    /// <summary>Stops the simulated API.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>Starts the simulated API again and waits until it is healthy.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task StartAsync(CancellationToken cancellationToken);
}
