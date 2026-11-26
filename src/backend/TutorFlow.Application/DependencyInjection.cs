using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TutorFlow.Application.Academic;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Assistant.Answering;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Authorization;
using TutorFlow.Application.Fallbacks;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Ports.Llm;

namespace TutorFlow.Application;

/// <summary>Composition of the Application layer (AGENTS §5, DUDA-01).</summary>
public static class DependencyInjection
{
    /// <summary>Registers the use case and its collaborators. <c>Program.cs</c> invokes it next to <c>AddInfrastructure()</c>.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// <para>Everything is scoped: one instance per HTTP request, so nothing survives between questions (R-RES-07) and
    /// the academic directory caches only the calls of its own request.</para>
    /// <para><b>Fallbacks:</b> until their owners register them, <see cref="ILlmClient"/> and
    /// <see cref="IRequestAuditContext"/> get safe defaults with <c>TryAdd</c>: an LLM that is always unavailable (every
    /// question that reaches the model is a 503, nothing is invented) and an audit context that keeps nothing. The real
    /// adapters must be registered with <c>Add…</c> (not <c>TryAdd…</c>), so the last registration wins whatever the
    /// order of <c>AddApplication()</c> and <c>AddInfrastructure()</c>.</para>
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<AssistantOptions>();
        services.AddScoped<IAcademicDirectory, AcademicDirectory>();
        services.AddScoped<IdentityVerifier>();
        services.AddScoped<StudentAuthorizer>();
        services.AddScoped<AssistantToolbox>();
        services.AddScoped<CitationValidator>();
        services.AddScoped<IAssistantService, AssistantService>();

        services.TryAddSingleton<ILlmClient, UnconfiguredLlmClient>();
        services.TryAddScoped<IRequestAuditContext, NullRequestAuditContext>();
        return services;
    }
}
