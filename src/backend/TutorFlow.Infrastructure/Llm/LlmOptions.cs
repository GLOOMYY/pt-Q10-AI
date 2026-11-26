using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TutorFlow.Infrastructure.Llm;

/// <summary>LLM settings read from environment variables (R-DEP-02, R-RES-05).</summary>
public sealed class LlmOptions
{
    /// <summary>Environment variable with the API key.</summary>
    public const string ApiKeyVariable = "LLM_API_KEY";

    /// <summary>Environment variable with the model name.</summary>
    public const string ModelVariable = "LLM_MODEL";

    /// <summary>Environment variable with the input price.</summary>
    public const string InputPriceVariable = "LLM_PRECIO_ENTRADA_USD_MTOK";

    /// <summary>Environment variable with the output price.</summary>
    public const string OutputPriceVariable = "LLM_PRECIO_SALIDA_USD_MTOK";

    /// <summary>LLM API key (<c>LLM_API_KEY</c>). Required; never logged (R-LOG-05).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model name (<c>LLM_MODEL</c>).</summary>
    public string Model { get; set; } = "gemini-3.1-flash-lite";

    /// <summary>USD per million input tokens (<c>LLM_PRECIO_ENTRADA_USD_MTOK</c>).</summary>
    public decimal InputPriceUsdPerMTok { get; set; } = 0.25m;

    /// <summary>USD per million output tokens, thinking included (<c>LLM_PRECIO_SALIDA_USD_MTOK</c>).</summary>
    public decimal OutputPriceUsdPerMTok { get; set; } = 1.50m;

    /// <summary>Timeout of one LLM call (R-QA-04: 30 s).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Sampling temperature (R-QA-03: 0 for stable answers).</summary>
    public double Temperature { get; set; }

    /// <summary>Root of the Gemini Developer API. Not read from the environment; tests and evals may override it.</summary>
    public Uri BaseUrl { get; set; } = new("https://generativelanguage.googleapis.com/");

    /// <summary>Problems found while reading the environment (unparsable prices); reported by the validator.</summary>
    public IList<string> BindingErrors { get; } = [];

    /// <summary>Reads the options from configuration. An invalid price is recorded, never silently defaulted.</summary>
    /// <param name="configuration">Configuration (environment variables included).</param>
    /// <param name="options">Instance to fill.</param>
    public static void Bind(IConfiguration configuration, LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        options.ApiKey = configuration[ApiKeyVariable] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(configuration[ModelVariable]))
        {
            options.Model = configuration[ModelVariable]!;
        }

        options.InputPriceUsdPerMTok = ReadPrice(configuration, InputPriceVariable, options.InputPriceUsdPerMTok, options);
        options.OutputPriceUsdPerMTok = ReadPrice(configuration, OutputPriceVariable, options.OutputPriceUsdPerMTok, options);
    }

    private static decimal ReadPrice(IConfiguration configuration, string key, decimal fallback, LlmOptions options)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback; // Variable not set: the documented default applies (R-DEP-02).
        }

        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        options.BindingErrors.Add($"{key} is not a valid decimal number.");
        return fallback;
    }
}

/// <summary>Validates <see cref="LlmOptions"/> so the backend fails to start with a clear message (R-DEP-02).</summary>
public sealed class LlmOptionsValidator : IValidateOptions<LlmOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>(options.BindingErrors);

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add($"{LlmOptions.ApiKeyVariable} is required but not set. Define it as an environment variable.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add($"{LlmOptions.ModelVariable} must not be empty.");
        }

        if (options.InputPriceUsdPerMTok < 0m)
        {
            failures.Add($"{LlmOptions.InputPriceVariable} must not be negative.");
        }

        if (options.OutputPriceUsdPerMTok < 0m)
        {
            failures.Add($"{LlmOptions.OutputPriceVariable} must not be negative.");
        }

        if (options.Timeout <= TimeSpan.Zero)
        {
            failures.Add("The LLM timeout must be positive.");
        }

        if (options.Temperature < 0)
        {
            failures.Add("The LLM temperature must not be negative.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>Registration of the LLM settings and of the real adapter.</summary>
public static class LlmServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="LlmOptions"/> with validation on start-up (R-DEP-02: no key, or an invalid price, and the
    /// backend does not start) and <see cref="TutorFlow.Application.Ports.Llm.ILlmClient"/> as the Gemini adapter
    /// created by <c>IHttpClientFactory</c> (the fake LLM is only used from <c>tests/</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration (environment variables included).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddLlm(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<LlmOptions>()
            .Configure(o => LlmOptions.Bind(configuration, o))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<LlmOptions>, LlmOptionsValidator>();
        services.AddHttpClient<TutorFlow.Application.Ports.Llm.ILlmClient, GeminiLlmClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<LlmOptions>>().Value;
            client.BaseAddress = options.BaseUrl;
            // The adapter enforces the timeout itself (R-QA-04) and maps it to a 503; the client one is only a backstop.
            client.Timeout = options.Timeout + TimeSpan.FromSeconds(5);
        });
        return services;
    }
}
