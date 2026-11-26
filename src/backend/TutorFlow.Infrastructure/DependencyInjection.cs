using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TutorFlow.Application.Ports.Clock;
using TutorFlow.Infrastructure.Clock;

namespace TutorFlow.Infrastructure;

/// <summary>LLM settings read from environment variables (R-DEP-02, R-RES-05).</summary>
public sealed class LlmOptions
{
    /// <summary>LLM API key (<c>LLM_API_KEY</c>). Required; never logged (R-LOG-05).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model name (<c>LLM_MODEL</c>).</summary>
    public string Model { get; set; } = "gemini-3.1-flash-lite";

    /// <summary>USD per million input tokens (<c>LLM_PRECIO_ENTRADA_USD_MTOK</c>).</summary>
    public decimal InputPriceUsdPerMTok { get; set; } = 0.25m;

    /// <summary>USD per million output tokens, thinking included (<c>LLM_PRECIO_SALIDA_USD_MTOK</c>).</summary>
    public decimal OutputPriceUsdPerMTok { get; set; } = 1.50m;
}

/// <summary>Simulated academic API settings (<c>MOCK_API_URL</c>).</summary>
public sealed class MockApiOptions
{
    /// <summary>Base URL of the simulated API as seen from the backend.</summary>
    public string BaseUrl { get; set; } = "http://mock-api:4000";
}

/// <summary>Composition of the Infrastructure adapters.</summary>
public static class DependencyInjection
{
    /// <summary>Registers configuration options and outbound adapters.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration (environment variables included).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LlmOptions>()
            .Configure(o =>
            {
                o.ApiKey = configuration["LLM_API_KEY"] ?? string.Empty;
                o.Model = Read(configuration, "LLM_MODEL", o.Model);
                o.InputPriceUsdPerMTok = ReadDecimal(configuration, "LLM_PRECIO_ENTRADA_USD_MTOK", o.InputPriceUsdPerMTok);
                o.OutputPriceUsdPerMTok = ReadDecimal(configuration, "LLM_PRECIO_SALIDA_USD_MTOK", o.OutputPriceUsdPerMTok);
            })
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
                "LLM_API_KEY is required but not set. Define it as an environment variable.");

        services.AddOptions<MockApiOptions>()
            .Configure(o => o.BaseUrl = Read(configuration, "MOCK_API_URL", o.BaseUrl));

        services.AddSingleton<IClock, SystemClock>();

        return services;
    }

    private static string Read(IConfiguration configuration, string key, string fallback) =>
        string.IsNullOrWhiteSpace(configuration[key]) ? fallback : configuration[key]!;

    private static decimal ReadDecimal(IConfiguration configuration, string key, decimal fallback) =>
        decimal.TryParse(configuration[key], NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
}
