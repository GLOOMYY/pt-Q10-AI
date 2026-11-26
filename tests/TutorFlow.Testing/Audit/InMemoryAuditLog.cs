using System.Collections.Concurrent;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Testing.Audit;

/// <summary>
/// In-memory <see cref="IAuditLog"/> for integration tests (R-LOG-09, D-30): nothing is written to stdout or to disk, and
/// the record of a request is found by its <c>X-Correlation-Id</c>, like the evals do with the file.
/// Register it as a singleton so every request of a test host writes to the same instance.
/// </summary>
public sealed class InMemoryAuditLog : IAuditLog
{
    private readonly ConcurrentQueue<AuditRecord> _records = new();

    /// <summary>All records written so far, in order.</summary>
    public IReadOnlyList<AuditRecord> Records => _records.ToArray();

    /// <inheritdoc />
    public Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records.Enqueue(record);
        return Task.CompletedTask;
    }

    /// <summary>The records written for a correlation id (a correct run has exactly one: R-LOG-01).</summary>
    /// <param name="correlationId">Value of the <c>X-Correlation-Id</c> header.</param>
    /// <returns>The matching records, possibly empty.</returns>
    public IReadOnlyList<AuditRecord> FindAll(string correlationId) =>
        _records.Where(r => string.Equals(r.CorrelationId, correlationId, StringComparison.Ordinal)).ToArray();

    /// <summary>The single record of a correlation id.</summary>
    /// <param name="correlationId">Value of the <c>X-Correlation-Id</c> header.</param>
    /// <returns>The record.</returns>
    /// <exception cref="InvalidOperationException">There is not exactly one record for the id (R-LOG-01).</exception>
    public AuditRecord GetByCorrelationId(string correlationId)
    {
        var found = FindAll(correlationId);
        return found.Count == 1
            ? found[0]
            : throw new InvalidOperationException(
                $"Expected exactly one audit record for correlation id '{correlationId}' but found {found.Count}.");
    }

    /// <summary>Removes every record.</summary>
    public void Clear() => _records.Clear();
}
