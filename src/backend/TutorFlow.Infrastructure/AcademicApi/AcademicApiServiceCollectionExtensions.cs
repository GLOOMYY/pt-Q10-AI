using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TutorFlow.Application.Ports.AcademicApi;

namespace TutorFlow.Infrastructure.AcademicApi;

/// <summary>Registration of the simulated academic API adapter.</summary>
public static class AcademicApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AcademicApiOptions"/> (from <c>MOCK_API_URL</c> and <c>MOCK_API_TIMEOUT_SECONDS</c>) and
    /// <see cref="IAcademicApi"/> as a typed <see cref="HttpClient"/> created by <c>IHttpClientFactory</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration (environment variables included).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAcademicApi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<AcademicApiOptions>().Configure(o =>
        {
            var url = configuration[AcademicApiOptions.BaseUrlKey];
            if (!string.IsNullOrWhiteSpace(url))
            {
                o.BaseUrl = url.Trim();
            }

            if (double.TryParse(configuration[AcademicApiOptions.TimeoutSecondsKey], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
            {
                o.Timeout = TimeSpan.FromSeconds(seconds);
            }
        });

        services.AddHttpClient<IAcademicApi, HttpAcademicApi>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<AcademicApiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = options.Timeout;
        });

        return services;
    }
}
