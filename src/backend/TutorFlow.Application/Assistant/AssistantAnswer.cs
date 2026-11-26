using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Assistant;

/// <summary>Kind of result of the use case; the inbound adapter maps it to the HTTP status (SPEC §5, §5.0).</summary>
public enum AssistantOutcome
{
    /// <summary>200: answered.</summary>
    Answered = 0,

    /// <summary>400: the body does not follow the contract.</summary>
    InvalidRequest = 1,

    /// <summary>403: not authorized or invalid identity.</summary>
    Forbidden = 2,

    /// <summary>503: the simulated API or the LLM is not available.</summary>
    Unavailable = 3,
}

/// <summary>
/// Result of the use case. Errors keep the shape of the successful output (SPEC §5.0, D-08) and are built with the
/// factory methods, which set the fixed texts and citations.
/// </summary>
/// <remarks>
/// The use case passes it to <see cref="IRequestAuditContext.Complete"/>, which takes <c>motivo</c>, <c>citas</c>,
/// <c>tokens</c> and <c>costoUsd</c> from it. The consulted students, routes and tools are not carried here: they are
/// recorded in the <see cref="IRequestAuditContext"/> when each call happens (R-LOG-04).
/// </remarks>
public sealed record AssistantAnswer
{
    /// <summary>Fixed text of the 403 (SPEC §5.0).</summary>
    public const string ForbiddenText = "No estás autorizado a obtener esa información.";

    /// <summary>Fixed text of the 400 (SPEC §5.0).</summary>
    public const string InvalidRequestText = "La solicitud no es válida.";

    /// <summary>Fixed text of the 503 (SPEC §5.0, API-02).</summary>
    public const string UnavailableText = "Los datos no están disponibles en este momento. Intenta más tarde.";

    /// <summary>The only citation of every 403: Reglamento 2025, Article 22 (SPEC §5.0).</summary>
    public static Citation ForbiddenCitation { get; } = new("reglamento_academico_2025.md", "Artículo 22");

    private AssistantAnswer(
        AssistantOutcome outcome,
        string text,
        IReadOnlyList<Citation> citations,
        bool usedStudentData,
        int tokens,
        decimal costUsd,
        string? reason)
    {
        if (tokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokens), "Tokens cannot be negative.");
        }

        if (costUsd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costUsd), "Cost cannot be negative.");
        }

        Outcome = outcome;
        Text = text;
        Citations = citations;
        UsedStudentData = usedStudentData;
        Tokens = tokens;

        // R-RES-05: the total cost is rounded to 6 decimals, half away from zero (D-06).
        CostUsd = Math.Round(costUsd, 6, MidpointRounding.AwayFromZero);
        Reason = reason;
    }

    /// <summary>Kind of result.</summary>
    public AssistantOutcome Outcome { get; }

    /// <summary><c>respuesta</c>, always in Spanish (R-RES-06).</summary>
    public string Text { get; }

    /// <summary><c>citas</c>, already validated (R-ARQ-03).</summary>
    public IReadOnlyList<Citation> Citations { get; }

    /// <summary><c>usoDatosEstudiante</c> (R-RES-04); always <see langword="false"/> for errors.</summary>
    public bool UsedStudentData { get; }

    /// <summary><c>tokens</c>: sum of <c>totalTokenCount</c> of the LLM calls of the request (R-RES-05).</summary>
    public int Tokens { get; }

    /// <summary><c>costoUsd</c>, rounded to 6 decimals (R-RES-05).</summary>
    public decimal CostUsd { get; }

    /// <summary>
    /// Audit <c>motivo</c> (one of <see cref="AuditReasons"/>); <see langword="null"/> when answered (R-LOG-10). It reaches
    /// the record through <see cref="IRequestAuditContext.Complete"/>; it is never sent to the client.
    /// </summary>
    public string? Reason { get; }

    /// <summary>200 with the answer written from authorized data.</summary>
    /// <param name="text">Answer text.</param>
    /// <param name="citations">Validated citations.</param>
    /// <param name="usedStudentData">Whether academic data of a student was consulted to answer (R-RES-04).</param>
    /// <param name="tokens">Total tokens.</param>
    /// <param name="costUsd">Total cost, not yet rounded.</param>
    /// <returns>The answer.</returns>
    public static AssistantAnswer Answered(
        string text,
        IReadOnlyList<Citation> citations,
        bool usedStudentData,
        int tokens,
        decimal costUsd) =>
        new(AssistantOutcome.Answered, text, citations, usedStudentData, tokens, costUsd, reason: null);

    /// <summary>400 with the standard body; never reaches the LLM, so tokens and cost are 0.</summary>
    /// <returns>The answer.</returns>
    public static AssistantAnswer InvalidRequest() =>
        new(AssistantOutcome.InvalidRequest, InvalidRequestText, [], false, 0, 0m, AuditReasons.InvalidRequest);

    /// <summary>403 with the standard body and the Article 22 citation.</summary>
    /// <param name="reason">Audit reason (one of <see cref="AuditReasons"/>).</param>
    /// <param name="tokens">Tokens of the LLM calls already made (0 if rejected before the LLM).</param>
    /// <param name="costUsd">Cost of those calls.</param>
    /// <returns>The answer.</returns>
    public static AssistantAnswer Forbidden(string reason, int tokens = 0, decimal costUsd = 0m) =>
        new(AssistantOutcome.Forbidden, ForbiddenText, [ForbiddenCitation], false, tokens, costUsd, reason);

    /// <summary>503 with the standard body; no data is invented (API-02).</summary>
    /// <param name="tokens">Tokens of the LLM calls already made.</param>
    /// <param name="costUsd">Cost of those calls.</param>
    /// <returns>The answer.</returns>
    public static AssistantAnswer Unavailable(int tokens = 0, decimal costUsd = 0m) =>
        new(AssistantOutcome.Unavailable, UnavailableText, [], false, tokens, costUsd, AuditReasons.DependencyUnavailable);
}
