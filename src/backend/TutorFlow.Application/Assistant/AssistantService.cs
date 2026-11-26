using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TutorFlow.Application.Assistant.Answering;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Ports.Llm;

namespace TutorFlow.Application.Assistant;

/// <summary>
/// The assistant use case. Order (SPEC §5, R-ARQ): contract validation (400) → identity format and existence
/// (403, R-SEG-04) → deterministic guards before the LLM (<see cref="IQuestionGuard"/>, R-SEG-14) → LLM with tools
/// (R-ARQ-01..02, R-ARQ-04) → citation validation (R-ARQ-03) → answer.
/// </summary>
/// <remarks>
/// <para>Every outcome, errors included, is handed once to <see cref="IRequestAuditContext.Complete"/>; the security
/// middleware writes the single audit record (R-LOG-01). The simulated API or the LLM not answering becomes a 503 and
/// nothing is invented (API-02, CL-13, CL-23).</para>
/// <para>Scoped: one instance per request, so nothing survives between questions (R-RES-07).</para>
/// </remarks>
public sealed partial class AssistantService : IAssistantService
{
    // R-QA-03: temperature 0 for stable answers.
    private const double Temperature = 0;

    private readonly IdentityVerifier _identityVerifier;
    private readonly IEnumerable<IQuestionGuard> _guards;
    private readonly ILlmClient _llm;
    private readonly AssistantToolbox _toolbox;
    private readonly CitationValidator _citations;
    private readonly IRequestAuditContext _audit;
    private readonly AssistantOptions _options;
    private readonly ILogger<AssistantService> _logger;

    /// <summary>Creates the use case.</summary>
    /// <param name="identityVerifier">Identity existence check (R-SEG-04).</param>
    /// <param name="guards">Deterministic guards run before the LLM, in registration order.</param>
    /// <param name="llm">The language model port.</param>
    /// <param name="toolbox">The tools exposed to the model.</param>
    /// <param name="citations">The citation validator.</param>
    /// <param name="audit">The audit context of the request.</param>
    /// <param name="options">The use case settings.</param>
    /// <param name="logger">Technical logger (never prompts, data or secrets, R-LOG-05).</param>
    public AssistantService(
        IdentityVerifier identityVerifier,
        IEnumerable<IQuestionGuard> guards,
        ILlmClient llm,
        AssistantToolbox toolbox,
        CitationValidator citations,
        IRequestAuditContext audit,
        IOptions<AssistantOptions> options,
        ILogger<AssistantService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _identityVerifier = identityVerifier;
        _guards = guards;
        _llm = llm;
        _toolbox = toolbox;
        _citations = citations;
        _audit = audit;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AssistantAnswer> AskAsync(AskQuestionInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var answer = await AnswerAsync(input, cancellationToken).ConfigureAwait(false);

        // R-LOG-01: reason, citations, tokens and cost of every outcome reach the single audit record.
        _audit.Complete(answer);
        return answer;
    }

    private async Task<AssistantAnswer> AnswerAsync(AskQuestionInput input, CancellationToken cancellationToken)
    {
        // SPEC §5.0: a body that does not follow the contract is a 400, before any dependency is called.
        if (!AskQuestionValidator.TryValidate(input, out var command))
        {
            return AssistantAnswer.InvalidRequest();
        }

        // R-SEG-04, API-01: with an invalid format (or a role/id mismatch) the API is never called.
        if (!UserIdentity.TryCreate(command.Role, command.DeclaredUserId, out var identity))
        {
            return AssistantAnswer.Forbidden(AuditReasons.InvalidIdentity);
        }

        SessionUser? user;
        try
        {
            user = await _identityVerifier.VerifyAsync(identity, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                // D-08: same 403 whatever the cause, so the existence of an id is never revealed.
                return AssistantAnswer.Forbidden(AuditReasons.InvalidIdentity);
            }

            // R-SEG-14: deterministic checks before the LLM; a block costs 0 tokens.
            foreach (var guard in _guards)
            {
                var verdict = await guard.EvaluateAsync(new GuardContext(user, command.Question), cancellationToken)
                    .ConfigureAwait(false);
                if (verdict.IsBlocked)
                {
                    return AssistantAnswer.Forbidden(verdict.Reason!);
                }
            }
        }
        catch (DependencyUnavailableException ex)
        {
            LogDependencyUnavailable(_logger, ex.Dependency, ex);
            return AssistantAnswer.Unavailable();
        }

        return await AnswerWithModelAsync(user, command.Question, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AssistantAnswer> AnswerWithModelAsync(
        SessionUser user, string question, CancellationToken cancellationToken)
    {
        var instructions = SystemInstructions.Build(user);
        var tools = AssistantToolbox.DefinitionsFor(user);
        var messages = new List<LlmMessage> { LlmMessage.User(question) };
        var state = new ToolRunState();
        var tokens = 0;
        var cost = 0m;

        try
        {
            for (var call = 0; call < _options.MaxLlmCalls; call++)
            {
                var request = new LlmRequest(instructions, [.. messages], tools, Temperature, ModelAnswer.Schema);
                var response = await _llm.CompleteAsync(request, cancellationToken).ConfigureAwait(false);

                // R-RES-05: every call made for this question counts, whatever the final outcome.
                tokens += response.Usage.TotalTokens;
                cost += response.Usage.CostUsd;

                if (response.ToolCalls.Count == 0)
                {
                    return Finish(response.Text, state, tokens, cost);
                }

                var outcome = await RunToolsAsync(user, response, messages, state, tokens, cost, cancellationToken)
                    .ConfigureAwait(false);
                if (outcome is not null)
                {
                    return outcome;
                }
            }

            LogToolLoopExhausted(_logger, _options.MaxLlmCalls);
            return AssistantAnswer.Unavailable(tokens, cost);
        }
        catch (DependencyUnavailableException ex)
        {
            // API-02, CL-13, CL-23: never an answer without its data or without the model.
            LogDependencyUnavailable(_logger, ex.Dependency, ex);
            return AssistantAnswer.Unavailable(tokens, cost);
        }
    }

    /// <summary>
    /// Runs the tool calls of one model turn. Returns a final answer when the turn ends the request (<c>rechazar</c> or a
    /// request for non-authorized data, both 403), or <see langword="null"/> to continue the loop.
    /// </summary>
    private async Task<AssistantAnswer?> RunToolsAsync(
        SessionUser user,
        LlmResponse response,
        List<LlmMessage> messages,
        ToolRunState state,
        int tokens,
        decimal cost,
        CancellationToken cancellationToken)
    {
        // R-ARQ-04, D-32: a turn that calls `rechazar` ends the request; no other call of that turn is run.
        var reject = response.ToolCalls.FirstOrDefault(c => c.Name == AssistantTools.Reject);
        if (reject is not null)
        {
            _audit.RecordTool(reject.Name);
            var rejected = await _toolbox.ExecuteAsync(user, reject, state, cancellationToken).ConfigureAwait(false);
            return AssistantAnswer.Forbidden(rejected.Reason!, tokens, cost);
        }

        messages.Add(LlmMessage.Model(response.Text, response.ToolCalls));
        var results = new List<LlmToolResult>(response.ToolCalls.Count);
        foreach (var toolCall in response.ToolCalls)
        {
            _audit.RecordTool(toolCall.Name);
            var execution = await _toolbox.ExecuteAsync(user, toolCall, state, cancellationToken).ConfigureAwait(false);
            if (execution.Kind != ToolExecutionKind.Completed)
            {
                // R-SEG-06: the model asked for data the user may not see: the whole answer is a 403 (R-SEG-07).
                return AssistantAnswer.Forbidden(execution.Reason!, tokens, cost);
            }

            results.Add(new LlmToolResult(toolCall.Name, execution.Result, toolCall.Id));
        }

        messages.Add(LlmMessage.Tool(results));
        return null;
    }

    private AssistantAnswer Finish(string? text, ToolRunState state, int tokens, decimal cost)
    {
        var proposed = ModelAnswer.Parse(text);
        if (proposed is null)
        {
            // An empty final reply is an unusable answer from the model: never invent one.
            LogEmptyAnswer(_logger);
            return AssistantAnswer.Unavailable(tokens, cost);
        }

        var validated = _citations.Validate(proposed, state);
        return AssistantAnswer.Answered(validated.Text, validated.Citations, state.UsedStudentData, tokens, cost);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dependency unavailable while answering: {Dependency}.")]
    private static partial void LogDependencyUnavailable(ILogger logger, ExternalDependency dependency, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The model did not give a final answer within {MaxCalls} calls.")]
    private static partial void LogToolLoopExhausted(ILogger logger, int maxCalls);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The model returned an empty final answer.")]
    private static partial void LogEmptyAnswer(ILogger logger);
}
