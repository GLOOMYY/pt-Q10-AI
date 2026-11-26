using TutorFlow.Application.Security.Defenses;

namespace TutorFlow.Application.Assistant;

/// <summary>
/// Adapter from the use case port <see cref="IQuestionGuard"/> to the deterministic defenses of seguridad
/// (<see cref="IQuestionDefense"/>: evident injection, system extraction and the name and id barrier, R-SEG-13, R-SEG-14,
/// D-28). A blocking defense becomes <see cref="GuardVerdict.Blocked"/> with its reason, so the use case answers 403 with
/// <c>tokens = 0</c> before calling the LLM.
/// </summary>
/// <remarks>
/// It receives every registered <see cref="IQuestionDefense"/> (normally the single <see cref="QuestionDefenseChain"/>
/// of <c>AddSecurityDefenses()</c>), so it can be resolved even when no defense is registered: then every question is
/// allowed and only the authorization of the tools applies (R-SEG-01..06).
/// </remarks>
public sealed class DefenseGuard : IQuestionGuard
{
    private readonly IEnumerable<IQuestionDefense> _defenses;

    /// <summary>Creates the adapter.</summary>
    /// <param name="defenses">The registered defenses, in registration order.</param>
    public DefenseGuard(IEnumerable<IQuestionDefense> defenses) => _defenses = defenses;

    /// <inheritdoc />
    public async Task<GuardVerdict> EvaluateAsync(GuardContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var defense in _defenses)
        {
            var verdict = await defense.EvaluateAsync(context.User.Identity, context.Question, cancellationToken)
                .ConfigureAwait(false);
            if (!verdict.IsAllowed)
            {
                return GuardVerdict.Blocked(verdict.Reason!);
            }
        }

        return GuardVerdict.Allowed;
    }
}
