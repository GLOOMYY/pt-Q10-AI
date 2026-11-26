using Microsoft.Extensions.DependencyInjection;
using TutorFlow.Application.Identity;

namespace TutorFlow.Application.Security.Defenses;

/// <summary>
/// The deterministic defenses in their order: evident injection, system extraction, and last the name barrier. The
/// cheap checks run first and never need the simulated API; the first one that blocks decides the reason. If none
/// blocks, the question goes on to the LLM, whose tools still authorize every datum (R-SEG-01..06).
/// </summary>
public sealed class QuestionDefenseChain : IQuestionDefense
{
    private readonly IReadOnlyList<IQuestionDefense> _defenses;

    /// <summary>Creates the chain with the registered defenses.</summary>
    /// <param name="injection">Evident injection (CL-01, AC-01).</param>
    /// <param name="extraction">System extraction (R-SEG-13).</param>
    /// <param name="barrier">Name and id barrier (R-SEG-14).</param>
    public QuestionDefenseChain(InjectionDefense injection, SystemExtractionDefense extraction, DeterministicBarrier barrier)
    {
        _defenses = [injection, extraction, barrier];
    }

    /// <inheritdoc />
    public async Task<DefenseVerdict> EvaluateAsync(UserIdentity user, string question, CancellationToken cancellationToken)
    {
        foreach (var defense in _defenses)
        {
            var verdict = await defense.EvaluateAsync(user, question, cancellationToken).ConfigureAwait(false);
            if (!verdict.IsAllowed)
            {
                return verdict;
            }
        }

        return DefenseVerdict.Allow;
    }
}

/// <summary>Registration of the deterministic defenses.</summary>
public static class SecurityDefensesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the defenses and exposes the whole chain as <see cref="IQuestionDefense"/>. The barrier is scoped because
    /// it uses the per-request audit context; the text defenses are stateless.
    /// <c>Program.cs</c> (plataforma) invokes it with <c>AddApplication()</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSecurityDefenses(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<InjectionDefense>();
        services.AddSingleton<SystemExtractionDefense>();
        services.AddScoped<DeterministicBarrier>();
        services.AddScoped<IQuestionDefense, QuestionDefenseChain>();
        return services;
    }
}
