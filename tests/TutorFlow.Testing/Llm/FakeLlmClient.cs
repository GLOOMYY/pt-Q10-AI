using TutorFlow.Application.Ports;
using TutorFlow.Application.Ports.Llm;

namespace TutorFlow.Testing.Llm;

/// <summary>
/// Deterministic, scriptable <see cref="ILlmClient"/> for <c>tests/</c> (AGENTS section 8). It never touches the network.
/// Queue replies with <see cref="Enqueue"/>; each call to <see cref="CompleteAsync"/> consumes one. Every request is
/// recorded so a test can assert that the model was (or was not) called, e.g. after the deterministic barrier (R-SEG-14).
/// </summary>
/// <remarks>
/// An unscripted call throws <see cref="InvalidOperationException"/> instead of inventing an answer, so a test that
/// unexpectedly reaches the LLM fails loudly. Use <see cref="SimulateQuotaExhausted"/> for the
/// <c>llm-cuota-agotada</c> precondition (CL-23, 429).
/// </remarks>
public sealed class FakeLlmClient : ILlmClient
{
    private readonly object _gate = new();
    private readonly Queue<FakeLlmReply> _script = new();
    private readonly List<LlmRequest> _requests = [];
    private Func<LlmRequest, FakeLlmReply>? _handler;
    private Exception? _failure;

    /// <summary>USD per million input tokens used for the reported cost (R-RES-05 defaults).</summary>
    public decimal InputPriceUsdPerMTok { get; set; } = 0.25m;

    /// <summary>USD per million output tokens used for the reported cost (R-RES-05 defaults).</summary>
    public decimal OutputPriceUsdPerMTok { get; set; } = 1.50m;

    /// <summary>
    /// D-32 across turns (default <see langword="true"/>): <c>rechazar</c> is decided before any data tool, so a reply
    /// calling it after a previous turn already called a data tool is a script error and throws
    /// <see cref="InvalidOperationException"/>. Set it to <see langword="false"/> to script a model that queries data
    /// and then rejects, e.g. to check that the audit log reports what was really queried (R-LOG-04). Within a single
    /// reply, <see cref="FakeLlmReply.FromToolCalls"/> always enforces the order.
    /// </summary>
    public bool EnforceRejectBeforeDataTools { get; set; } = true;

    /// <summary>Requests received so far, in order.</summary>
    public IReadOnlyList<LlmRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToList();
            }
        }
    }

    /// <summary>Number of calls received (0 proves the LLM was never reached).</summary>
    public int CallCount => Requests.Count;

    /// <summary>Queues the next reply.</summary>
    /// <param name="reply">The reply.</param>
    /// <returns>This instance, for chaining.</returns>
    public FakeLlmClient Enqueue(FakeLlmReply reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        lock (_gate)
        {
            _script.Enqueue(reply);
        }

        return this;
    }

    /// <summary>Queues a final text reply.</summary>
    /// <param name="text">The text.</param>
    /// <returns>This instance, for chaining.</returns>
    public FakeLlmClient EnqueueText(string text) => Enqueue(FakeLlmReply.FromText(text));

    /// <summary>Queues a reply that calls <c>rechazar(motivo)</c> (R-ARQ-04).</summary>
    /// <param name="reason">The rejection reason.</param>
    /// <returns>This instance, for chaining.</returns>
    public FakeLlmClient EnqueueReject(string reason) => Enqueue(FakeLlmReply.Reject(reason));

    /// <summary>Queues a reply that calls tools (<c>rechazar</c>, if present, must come first: D-32).</summary>
    /// <param name="calls">The tool calls.</param>
    /// <returns>This instance, for chaining.</returns>
    public FakeLlmClient EnqueueToolCalls(params LlmToolCall[] calls) => Enqueue(FakeLlmReply.FromToolCalls(calls));

    /// <summary>
    /// Answers every call with a function of the request instead of the queue (the queue is ignored while a handler is
    /// set). Useful for loops whose number of turns the test does not want to count.
    /// </summary>
    /// <param name="handler">Function producing the reply.</param>
    /// <returns>This instance, for chaining.</returns>
    public FakeLlmClient Respond(Func<LlmRequest, FakeLlmReply> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            _handler = handler;
        }

        return this;
    }

    /// <summary>
    /// Makes every call fail as an exhausted quota (HTTP 429, CL-23): throws a
    /// <see cref="DependencyUnavailableException"/> for <see cref="ExternalDependency.Llm"/>. The request is still recorded.
    /// </summary>
    /// <returns>This instance, for chaining.</returns>
    public FakeLlmClient SimulateQuotaExhausted() => SimulateFailure("Simulated LLM quota exhausted (HTTP 429).");

    /// <summary>Makes every call fail as an unavailable LLM (e.g. timeout, R-QA-04).</summary>
    /// <param name="message">Technical message of the exception.</param>
    /// <returns>This instance, for chaining.</returns>
    public FakeLlmClient SimulateFailure(string message)
    {
        lock (_gate)
        {
            _failure = new DependencyUnavailableException(ExternalDependency.Llm, message);
        }

        return this;
    }

    /// <inheritdoc />
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        FakeLlmReply reply;
        lock (_gate)
        {
            _requests.Add(request);
            if (_failure is not null)
            {
                throw _failure;
            }

            if (_handler is not null)
            {
                reply = _handler(request);
            }
            else if (!_script.TryDequeue(out reply!))
            {
                throw new InvalidOperationException(
                    $"FakeLlmClient received call #{_requests.Count} but the script has no reply left.");
            }
        }

        if (EnforceRejectBeforeDataTools
            && reply.ToolCalls.Any(c => c.Name == FakeLlmReply.RejectToolName)
            && request.Messages.Any(m => m.Role == LlmRole.Model
                && m.ToolCalls.Any(c => c.Name != FakeLlmReply.RejectToolName)))
        {
            throw new InvalidOperationException(
                "D-32: 'rechazar' cannot come after a data tool already called in this request. "
                + "Set EnforceRejectBeforeDataTools to false to script that on purpose.");
        }

        // R-RES-05: cost of the call with the configured prices, not rounded (shared LlmCost formula).
        var total = reply.PromptTokens + reply.CandidatesTokens + reply.ThoughtsTokens;
        var usage = new LlmUsage(
            reply.PromptTokens,
            reply.CandidatesTokens,
            reply.ThoughtsTokens,
            total,
            LlmCost.Compute(
                reply.PromptTokens,
                reply.CandidatesTokens,
                reply.ThoughtsTokens,
                InputPriceUsdPerMTok,
                OutputPriceUsdPerMTok));
        return Task.FromResult(new LlmResponse(reply.Text, reply.ToolCalls, usage));
    }
}
