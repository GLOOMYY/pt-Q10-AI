using Microsoft.Extensions.DependencyInjection;
using TutorFlow.Application.Assistant;

namespace TutorFlow.Application;

/// <summary>Composition of the Application layer (AGENTS §5, DUDA-01).</summary>
public static class DependencyInjection
{
    /// <summary>Registers the use cases. <c>Program.cs</c> invokes it next to <c>AddInfrastructure()</c>.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped: one instance per HTTP request, so nothing survives between questions (R-RES-07).
        services.AddScoped<IAssistantService, AssistantService>();
        return services;
    }
}
