using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TutorFlow.Application.Ports.Clock;
using TutorFlow.Infrastructure.AcademicApi;
using TutorFlow.Infrastructure.Audit;
using TutorFlow.Infrastructure.Clock;
using TutorFlow.Infrastructure.Knowledge;
using TutorFlow.Infrastructure.Llm;

namespace TutorFlow.Infrastructure;

/// <summary>Composition of the Infrastructure adapters.</summary>
public static class DependencyInjection
{
    /// <summary>Registers configuration options and outbound adapters.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration (environment variables included).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // R-DEP-02: AddLlm validates LLM_API_KEY on start-up.
        services.AddAcademicApi(configuration);
        services.AddKnowledge(configuration);
        services.AddLlm(configuration);
        services.AddAuditLog(configuration);

        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
