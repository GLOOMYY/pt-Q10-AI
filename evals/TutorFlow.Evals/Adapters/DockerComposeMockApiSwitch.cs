using System.Diagnostics;
using TutorFlow.Evals.Ports;

namespace TutorFlow.Evals.Adapters;

/// <summary>
/// Stops and starts the <c>mock-api</c> service with <c>docker compose</c> (README section 2.1, <c>api-simulada-detenida</c>).
/// Commands run in the repository root, never wait for input and are always bounded by a timeout.
/// </summary>
public sealed class DockerComposeMockApiSwitch(string repositoryRoot, string service = "mock-api") : IMockApiSwitch
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan HealthyTimeout = TimeSpan.FromSeconds(90);

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken) =>
        await RunAsync(["compose", "stop", service], cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RunAsync(["compose", "start", service], cancellationToken).ConfigureAwait(false);
        var deadline = DateTimeOffset.UtcNow + HealthyTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var id = (await RunAsync(["compose", "ps", "-q", service], cancellationToken).ConfigureAwait(false)).Trim();
            if (id.Length > 0)
            {
                var health = (await RunAsync(["inspect", "--format", "{{.State.Health.Status}}", id], cancellationToken).ConfigureAwait(false)).Trim();
                if (health == "healthy")
                {
                    return;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"The '{service}' container did not become healthy within {HealthyTimeout.TotalSeconds:0} s.");
    }

    private async Task<string> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("docker")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("docker could not be started.");
        process.StandardInput.Close(); // Nothing is ever read from stdin: the command can never wait for it.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CommandTimeout);
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"docker {string.Join(' ', arguments)} failed ({process.ExitCode}): {(await error.ConfigureAwait(false)).Trim()}");
            }

            return await output.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Only this process (by its own handle), never others by name.
            process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
