using System.Text;
using Microsoft.Extensions.Options;
using TutorFlow.Application.Audit;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Infrastructure.Audit;

/// <summary>
/// Adapter of <see cref="IAuditLog"/>: writes one JSON line per request to stdout and appends it to the audit file
/// (R-LOG-07, D-30). It never exposes the log over HTTP. The line is built by <see cref="AuditJson"/>, so it has the
/// exact fields of R-LOG-08 and nothing else: no grades, names, answers, secrets or prompts (R-LOG-02, R-LOG-05).
/// </summary>
/// <remarks>
/// Register it as a singleton. Writes are serialized with a semaphore so concurrent requests never interleave their
/// lines, and each line is written with a single append of UTF-8 bytes (no BOM) ending in <c>\n</c>.
/// </remarks>
public sealed class FileAuditLog : IAuditLog, IDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _path;
    private readonly TextWriter _stdout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _directoryReady;

    /// <summary>Creates the adapter writing to stdout and to the configured file.</summary>
    /// <param name="options">Audit options (file path).</param>
    public FileAuditLog(IOptions<AuditLogOptions> options)
        : this(options, Console.Out)
    {
    }

    /// <summary>Creates the adapter with an explicit stdout writer (tests).</summary>
    /// <param name="options">Audit options (file path).</param>
    /// <param name="stdout">Where the line is echoed.</param>
    public FileAuditLog(IOptions<AuditLogOptions> options, TextWriter stdout)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(stdout);
        _path = Path.GetFullPath(options.Value.FilePath);
        _stdout = stdout;
    }

    /// <inheritdoc />
    public async Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        var line = AuditJson.Serialize(record);
        var bytes = Utf8NoBom.GetBytes(line + "\n");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // stdout first: it is the source in the container logs even if the volume is not writable.
            await _stdout.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _stdout.FlushAsync(cancellationToken).ConfigureAwait(false);

            EnsureDirectory();
            await using var stream = new FileStream(
                _path, FileMode.Append, FileAccess.Write, FileShare.Read, bufferSize: 1, FileOptions.Asynchronous);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private void EnsureDirectory()
    {
        if (_directoryReady)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _directoryReady = true;
    }
}
