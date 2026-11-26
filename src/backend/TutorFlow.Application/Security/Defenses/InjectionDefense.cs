using System.Text.RegularExpressions;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Security.Defenses;

/// <summary>
/// Deterministic detection of <b>evident</b> prompt injection written in the question (CL-01, AC-01, R-FUE-03,
/// R-SEG-09): orders to ignore the instructions and fake system or tool tags. It only fires on unmistakable phrasings;
/// everything subtler (aggregates, rankings, mixed questions, claims about who is asking, R-SEG-07..09) is left to the
/// model, which calls <c>rechazar</c>, and to the tools, which authorize every datum (R-SEG-01..06).
/// </summary>
/// <remarks>
/// It never touches the simulated API. Blocked questions get the reason <see cref="AuditReasons.InjectionAttempt"/>
/// (R-LOG-03). The patterns were checked against every question of <c>escenarios/</c> that must be answered, so they
/// produce no false positives there.
/// </remarks>
public sealed partial class InjectionDefense : IQuestionDefense
{
    /// <inheritdoc />
    public Task<DefenseVerdict> EvaluateAsync(UserIdentity user, string question, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        return Task.FromResult(IsInjection(question)
            ? DefenseVerdict.Block(AuditReasons.InjectionAttempt)
            : DefenseVerdict.Allow);
    }

    /// <summary>Whether the question contains an evident injection attempt.</summary>
    /// <param name="question">The question as received.</param>
    /// <returns><see langword="true"/> if an evident injection pattern is present.</returns>
    public static bool IsInjection(string question)
    {
        var text = TextNormalizer.Normalize(question);
        return IsOverrideOrder(text) || FakeRoleTag().IsMatch(text) || KnownJailbreak().IsMatch(text);
    }

    // "ignora todas tus instrucciones anteriores", "ignore all previous instructions", "olvida tus reglas".
    // The verb must be followed, within a few words, by the thing to ignore AND by a scope word (all, previous, your...),
    // so "¿puedo ignorar las reglas de asistencia?" style questions are not matched (imperative forms only).
    [GeneratedRegex(
        @"\b(?:ignora|ignore|olvida|olvidate|forget|disregard)\b(?:\W+\w+){0,4}?\W+(?:instrucciones|instructions|prompt|reglas|rules|directrices)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex OverrideOrder();

    [GeneratedRegex(
        @"\b(?:todas|todo|all|any|anteriores|previous|prior|above|arriba|tus|your|sus|del sistema|system)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex ScopeWord();

    private static bool IsOverrideOrder(string text)
    {
        foreach (Match match in OverrideOrder().Matches(text))
        {
            if (ScopeWord().IsMatch(match.Value))
            {
                return true;
            }
        }

        return false;
    }

    // Fake chat roles: "</user><system>...", "<assistant>", "<tool_result>".
    [GeneratedRegex(
        @"</?\s*(?:system|assistant|user|developer|tool|tool_result|instructions?)\b[^>]*>",
        RegexOptions.CultureInvariant)]
    private static partial Regex FakeRoleTag();

    [GeneratedRegex(
        @"\b(?:jailbreak|developer mode|modo desarrollador|do anything now)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex KnownJailbreak();
}
