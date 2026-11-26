using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Ports.Llm;

namespace TutorFlow.Infrastructure.Llm;

/// <summary>
/// Production adapter of <see cref="ILlmClient"/> over the Gemini Developer API (<c>generateContent</c>, REST).
/// The key travels only in the <c>x-goog-api-key</c> header (never in the URL, logs or exception messages, R-LOG-05).
/// Quota exhaustion (429, CL-23), 5xx, timeouts (R-QA-04) and network errors throw
/// <see cref="DependencyUnavailableException"/> for <see cref="ExternalDependency.Llm"/>.
/// </summary>
public sealed partial class GeminiLlmClient : ILlmClient
{
    /// <summary>Name of the header carrying the API key.</summary>
    public const string ApiKeyHeader = "x-goog-api-key";

    private const int MaxErrorDetailLength = 300;

    private readonly HttpClient _http;
    private readonly LlmOptions _options;
    private readonly ILogger<GeminiLlmClient> _logger;
    private readonly TimeProvider _time;

    /// <summary>Creates the adapter.</summary>
    /// <param name="http">Client whose <c>BaseAddress</c> is the API root.</param>
    /// <param name="options">LLM settings (model, prices, timeout, key).</param>
    /// <param name="logger">Technical logger; receives neither prompts nor the key.</param>
    /// <param name="timeProvider">Clock for the call budget and the retry backoff (system clock when omitted).</param>
    public GeminiLlmClient(
        HttpClient http, IOptions<LlmOptions> options, ILogger<GeminiLlmClient> logger, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var path = $"v1beta/models/{Uri.EscapeDataString(_options.Model)}:generateContent";
        // R-QA-04: the 30 s budget covers the whole call, retries included.
        using var budget = new CancellationTokenSource(_options.Timeout, _time);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, budget.Token);
        var started = _time.GetTimestamp();

        try
        {
            for (var attempt = 0; ; attempt++)
            {
                using var message = BuildMessage(path, request);
                using var response = await _http.SendAsync(message, timeout.Token);
                var body = await response.Content.ReadAsStringAsync(timeout.Token);

                if (IsTransient(response.StatusCode) && attempt < _options.MaxRetries)
                {
                    // Transient provider overload (DUDA-37): retry only while the budget has room for the wait.
                    var delay = ComputeBackoff(attempt);
                    var remaining = _options.Timeout - _time.GetElapsedTime(started);
                    if (remaining > delay)
                    {
                        LogRetry(_logger, (int)response.StatusCode, attempt + 1, _options.MaxRetries, delay.TotalMilliseconds);
                        await Task.Delay(delay, _time, timeout.Token);
                        continue;
                    }
                }

                return HandleResponse(response.StatusCode, body);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimeout(_logger, _options.Timeout.TotalSeconds);
            throw new DependencyUnavailableException(
                ExternalDependency.Llm, $"The LLM did not answer within {_options.Timeout.TotalSeconds:0.##} s.");
        }
        catch (HttpRequestException ex)
        {
            LogNetworkError(_logger, ex.GetType().Name);
            throw new DependencyUnavailableException(ExternalDependency.Llm, "The LLM could not be reached.", ex);
        }
    }

    /// <summary>Provider errors worth retrying: overload or transient server faults. 429 (quota, CL-23) and 4xx are not.</summary>
    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.ServiceUnavailable or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout;

    /// <summary>Exponential backoff (base * 2^attempt) with +-50% jitter.</summary>
    private TimeSpan ComputeBackoff(int attempt)
    {
        var jitter = 0.5 + Random.Shared.NextDouble();
        return TimeSpan.FromMilliseconds(_options.RetryBackoff.TotalMilliseconds * Math.Pow(2, attempt) * jitter);
    }

    private HttpRequestMessage BuildMessage(string path, LlmRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(GeminiRequestMapper.ToJson(request, _options.Model, _options.ThinkingLevel)),
        };
        message.Headers.TryAddWithoutValidation(ApiKeyHeader, _options.ApiKey);
        return message;
    }

    private LlmResponse HandleResponse(HttpStatusCode status, string body)
    {
        var code = (int)status;
        if (code is >= 200 and < 300)
        {
            try
            {
                return GeminiResponseMapper.Parse(body, _options.InputPriceUsdPerMTok, _options.OutputPriceUsdPerMTok);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                throw new DependencyUnavailableException(
                    ExternalDependency.Llm, "The LLM returned an unreadable or empty reply.", ex);
            }
        }

        LogFailure(_logger, code);
        var reason = status switch
        {
            HttpStatusCode.TooManyRequests => "quota exhausted (HTTP 429)",
            HttpStatusCode.BadRequest => "malformed request or invalid configuration (HTTP 400)",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"request rejected (HTTP {code}); check the API key",
            _ => $"HTTP {code}",
        };
        var detail = ExtractErrorDetail(body);
        throw new DependencyUnavailableException(
            ExternalDependency.Llm,
            detail is null ? $"LLM call failed: {reason}." : $"LLM call failed: {reason}: {detail}");
    }

    /// <summary>Reads <c>error.message</c> of the provider error body, truncated and with the key redacted.</summary>
    private string? ExtractErrorDetail(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var error)
                || !error.TryGetProperty("message", out var text)
                || text.GetString() is not { Length: > 0 } detail)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                detail = detail.Replace(_options.ApiKey, "[redacted]", StringComparison.Ordinal);
            }

            return detail.Length > MaxErrorDetailLength ? detail[..MaxErrorDetailLength] : detail;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM call timed out after {Seconds} s.")]
    private static partial void LogTimeout(ILogger logger, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM call failed with transient HTTP {StatusCode}; retry {Attempt}/{MaxRetries} in {DelayMs} ms.")]
    private static partial void LogRetry(ILogger logger, int statusCode, int attempt, int maxRetries, double delayMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM call failed with a network error ({ErrorType}).")]
    private static partial void LogNetworkError(ILogger logger, string errorType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM call failed with HTTP {StatusCode}.")]
    private static partial void LogFailure(ILogger logger, int statusCode);
}
