using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Security.Defenses;

/// <summary>
/// Outcome of a deterministic defense: the question may go on to the LLM, or it is blocked with a 403 and the audit
/// <c>motivo</c> (R-LOG-10). A blocked question never reaches the model: <c>tokens = 0</c>, <c>costoUsd = 0</c>.
/// </summary>
public sealed record DefenseVerdict
{
    private DefenseVerdict(bool isAllowed, string? reason)
    {
        IsAllowed = isAllowed;
        Reason = reason;
    }

    /// <summary>Verdict that lets the question continue.</summary>
    public static DefenseVerdict Allow { get; } = new(true, null);

    /// <summary>Whether the question may continue to the LLM.</summary>
    public bool IsAllowed { get; }

    /// <summary>Audit reason (one of <see cref="AuditReasons"/>) when blocked; <see langword="null"/> when allowed.</summary>
    public string? Reason { get; }

    /// <summary>Verdict that blocks the question with a 403.</summary>
    /// <param name="reason">One of <see cref="AuditReasons"/>.</param>
    /// <returns>The blocking verdict.</returns>
    public static DefenseVerdict Block(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new DefenseVerdict(false, reason);
    }
}

/// <summary>
/// A deterministic check of the question, run before the LLM (R-SEG-14, D-28). Its input is the already validated
/// identity (<see cref="UserIdentity"/>) and the question; the identity never comes from the text (R-SEG-09).
/// </summary>
/// <remarks>
/// The use case calls the defenses through its own <c>IQuestionGuard</c> port; an adapter maps that port to this
/// interface. The defenses only complement the authorization of the tools (R-SEG-01..06), they never replace it.
/// </remarks>
public interface IQuestionDefense
{
    /// <summary>Evaluates the question.</summary>
    /// <param name="user">The validated user of the session.</param>
    /// <param name="question">The question, already trimmed and validated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Allowed, or blocked with its reason.</returns>
    /// <exception cref="Ports.DependencyUnavailableException">The simulated API needed by a defense is not available.</exception>
    Task<DefenseVerdict> EvaluateAsync(UserIdentity user, string question, CancellationToken cancellationToken);
}
