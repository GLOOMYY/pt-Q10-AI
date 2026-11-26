namespace TutorFlow.Application.Assistant;

/// <summary>
/// Use case behind <c>POST /api/asistente/preguntar</c>. Order: validation → identity → deterministic barrier
/// (R-SEG-14) → LLM with tools → citation validation (R-ARQ-03) → answer.
/// </summary>
public interface IAssistantService
{
    /// <summary>Answers one question, independently of any other (R-RES-07).</summary>
    /// <param name="input">Raw input read by the inbound adapter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The answer, including errors with the contract shape (SPEC §5.0).</returns>
    Task<AssistantAnswer> AskAsync(AskQuestionInput input, CancellationToken cancellationToken);
}
