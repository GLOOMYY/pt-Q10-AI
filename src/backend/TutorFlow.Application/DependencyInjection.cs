using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TutorFlow.Application.Academic;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Assistant.Answering;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Authorization;
using TutorFlow.Application.Fallbacks;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports.Llm;

namespace TutorFlow.Application;

/// <summary>Composition of the Application layer (AGENTS §5, DUDA-01).</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the use case and its collaborators. <c>Program.cs</c> invokes it next to <c>AddAudit()</c>,
    /// <c>AddSecurityDefenses()</c> and <c>AddInfrastructure()</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// <para>Everything is scoped: one instance per HTTP request, so nothing survives between questions (R-RES-07) and
    /// the academic directory caches only the calls of its own request.</para>
    /// <para><see cref="AssistantOptions"/> is bound from the configuration section <see cref="AssistantOptions.Section"/>
    /// (e.g. the optional environment variable <c>Assistant__MaxLlmCalls</c>), with the defaults of the class (AGENTS §7).</para>
    /// <para>The deterministic defenses of seguridad run through <see cref="DefenseGuard"/> (R-SEG-13, R-SEG-14).</para>
    /// <para><b>Fallback:</b> until an LLM adapter is registered, <see cref="ILlmClient"/> gets, with <c>TryAdd</c>, an LLM
    /// that is always unavailable (every question that reaches the model is a 503; nothing is invented). The real adapter
    /// is registered with <c>Add…</c> and wins. The audit context has no fallback: <c>AddAudit()</c> must be called, so a
    /// missing registration fails on start-up instead of silently losing the log (R-LOG-01).</para>
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<AssistantOptions>()
            .BindConfiguration(AssistantOptions.Section)
            .Validate(o => o.MaxLlmCalls > 0, $"{AssistantOptions.Section}:{nameof(AssistantOptions.MaxLlmCalls)} must be positive.")
            .ValidateOnStart();
        services.AddScoped<IAcademicDirectory, AcademicDirectory>();
        services.AddScoped<IdentityVerifier>();
        services.AddScoped<StudentAuthorizer>();
        services.AddScoped<AssistantToolbox>();
        services.AddScoped<CitationValidator>();
        services.AddScoped<IQuestionGuard, DefenseGuard>();
        services.AddScoped<IAssistantService, AssistantService>();

        services.TryAddSingleton<ILlmClient, UnconfiguredLlmClient>();
        return services;
    }
}
