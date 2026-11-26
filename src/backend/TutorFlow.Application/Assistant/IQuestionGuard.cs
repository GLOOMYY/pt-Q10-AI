using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Assistant;

/// <summary>
/// Extension point for deterministic checks of the question that run <b>before the LLM</b>, such as the name and id
/// barrier of R-SEG-14 (D-28) or deterministic detections of injection or system extraction (R-SEG-13).
/// </summary>
/// <remarks>
/// <para><b>When it runs:</b> the use case (<see cref="AssistantService"/>) invokes every registered guard, in
/// registration order, after the contract validation (400) and the identity check (403/503), and before the first LLM
/// call. The first <see cref="GuardVerdict.Blocked"/> verdict ends the request with the standard 403 body (SPEC §5.0),
/// <c>tokens = 0</c>, <c>costoUsd = 0</c>, <c>usoDatosEstudiante = false</c> and the verdict reason as <c>motivo</c>;
/// the remaining guards are not invoked.</para>
/// <para><b>Registration:</b> as scoped or singleton <see cref="IQuestionGuard"/> services
/// (<c>services.AddScoped&lt;IQuestionGuard, MyGuard&gt;()</c>); several may be registered. With none, every question
/// goes to the LLM.</para>
/// <para><b>Data access:</b> a guard that needs program rosters (e.g. the barrier block list) must read them through
/// <see cref="Academic.IAcademicDirectory"/>, which caches the calls of the request and records them in
/// <c>recursosConsultados</c>. It must never read student academic data (grades or attendance): those calls would
/// count in <c>estudiantesConsultados</c> (R-LOG-06), and a barrier never consults students (R-SEG-14).</para>
/// <para><b>Failures:</b> a <see cref="Ports.DependencyUnavailableException"/> becomes a 503; any other exception
/// propagates as an unexpected error.</para>
/// </remarks>
public interface IQuestionGuard
{
    /// <summary>Evaluates the question of a verified user.</summary>
    /// <param name="context">The verified user and the trimmed question.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see cref="GuardVerdict.Allowed"/> to continue, or <see cref="GuardVerdict.Blocked"/> with a reason.</returns>
    Task<GuardVerdict> EvaluateAsync(GuardContext context, CancellationToken cancellationToken);
}

/// <summary>Input of an <see cref="IQuestionGuard"/>.</summary>
/// <param name="User">The verified session user: role, normalized id and programs (R-SEG-04). The identity only comes
/// from <c>usuario</c> in the body, never from the question (R-SEG-09).</param>
/// <param name="Question">The question as validated: trimmed, 1 to 1 000 characters (SPEC §5.0).</param>
public sealed record GuardContext(SessionUser User, string Question);

/// <summary>Verdict of an <see cref="IQuestionGuard"/>.</summary>
public sealed record GuardVerdict
{
    private static readonly HashSet<string> ForbiddenReasons = new(StringComparer.Ordinal)
    {
        AuditReasons.DeterministicBarrier,
        AuditReasons.ThirdPartyData,
        AuditReasons.InjectionAttempt,
        AuditReasons.SystemExtractionAttempt,
        AuditReasons.InvalidIdentity,
    };

    private GuardVerdict(string? reason) => Reason = reason;

    /// <summary>The question may continue to the next guard and then to the LLM.</summary>
    public static GuardVerdict Allowed { get; } = new(reason: null);

    /// <summary>Whether the question is blocked (403).</summary>
    public bool IsBlocked => Reason is not null;

    /// <summary>The audit <c>motivo</c> when blocked; <see langword="null"/> when allowed.</summary>
    public string? Reason { get; }

    /// <summary>Blocks the question with a 403 (SPEC §5.0).</summary>
    /// <param name="reason">A 403 reason of the closed catalog (R-LOG-10), usually
    /// <see cref="AuditReasons.DeterministicBarrier"/>, <see cref="AuditReasons.InjectionAttempt"/> or
    /// <see cref="AuditReasons.SystemExtractionAttempt"/>.</param>
    /// <returns>The verdict.</returns>
    /// <exception cref="ArgumentException">The reason is not a 403 reason of the catalog.</exception>
    public static GuardVerdict Blocked(string reason)
    {
        if (reason is null || !ForbiddenReasons.Contains(reason))
        {
            throw new ArgumentException("R-LOG-10: the reason must be a 403 value of the closed catalog.", nameof(reason));
        }

        return new GuardVerdict(reason);
    }
}
