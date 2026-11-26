using System.Text.Json;
using TutorFlow.Evals.Ports;

namespace TutorFlow.Evals.Adapters;

/// <summary>
/// Reads <c>auditoria.jsonl</c> (one JSON object per line) and finds the records of a correlation id. The file is opened
/// with shared access because the backend keeps writing to it.
/// </summary>
public sealed class JsonlAuditSource(string path) : IAuditSource
{
    /// <summary>Path of the file.</summary>
    public string Path { get; } = path;

    /// <inheritdoc />
    public async Task<IReadOnlyList<JsonElement>> FindAsync(string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(correlationId);
        var found = new List<JsonElement>();
        if (!File.Exists(Path))
        {
            return found;
        }

        await using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            // Cheap filter first: most lines are other requests.
            if (!line.Contains(correlationId, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("correlationId", out var id)
                    && id.ValueKind == JsonValueKind.String
                    && string.Equals(id.GetString(), correlationId, StringComparison.Ordinal))
                {
                    found.Add(document.RootElement.Clone());
                }
            }
            catch (JsonException)
            {
                // A line being written right now (partial): it is read again on the next poll.
            }
        }

        return found;
    }
}
