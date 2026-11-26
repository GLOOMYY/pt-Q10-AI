using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Assistant;

/// <summary>
/// Implementation of the assistant use case.
/// </summary>
/// <remarks>
/// <b>Provisional (F1a):</b> only the contract steps are implemented: validation (400) and the identity format check
/// (403). The rest of the pipeline (identity existence, barrier, LLM with tools, citations) is built in F2; until then
/// a valid request gets the standard 503 body, so no answer is ever invented (SPEC §8, Prohibition 1).
/// </remarks>
public sealed class AssistantService : IAssistantService
{
    /// <inheritdoc />
    public Task<AssistantAnswer> AskAsync(AskQuestionInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        // SPEC §5.0: a body that does not follow the contract is a 400, before any dependency is called.
        if (!AskQuestionValidator.TryValidate(input, out var command))
        {
            return Task.FromResult(AssistantAnswer.InvalidRequest());
        }

        // R-SEG-04, API-01: with an invalid format (or a role/id mismatch) the API is never called.
        if (!UserIdentity.TryCreate(command.Role, command.DeclaredUserId, out _))
        {
            return Task.FromResult(AssistantAnswer.Forbidden(AuditReasons.InvalidIdentity));
        }

        // F1a placeholder: the answering pipeline is not built yet (F2). Never invent an answer.
        return Task.FromResult(AssistantAnswer.Unavailable());
    }
}
