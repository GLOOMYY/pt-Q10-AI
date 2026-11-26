namespace TutorFlow.Application.Ports.Llm;

/// <summary>
/// Port to the language model. The model only reaches data through the tools the backend exposes (R-ARQ-01).
/// </summary>
/// <remarks>
/// <para><b>Implemented by:</b> <c>conocimiento</c>, with the real adapter in <c>TutorFlow.Infrastructure/Llm/</c>
/// (30 s timeout, R-QA-04; temperature as requested, R-QA-03; cost with the configured prices, R-RES-05) and the
/// deterministic fake LLM in <c>tests/TutorFlow.Testing/Llm/</c>.</para>
/// <para><b>Failures:</b> timeout, connection error, exhausted quota (e.g. HTTP 429, CL-23) or an unusable reply throw
/// <see cref="DependencyUnavailableException"/> with <see cref="ExternalDependency.Llm"/>, which becomes a 503
/// (SPEC §5.0). Tokens already spent by previous calls of the same request are still reported by the use case.</para>
/// <para>Implementations never log the prompts or the API key (R-LOG-05).</para>
/// </remarks>
public interface ILlmClient
{
    /// <summary>Sends one request to the model and returns its reply (text and/or tool calls) with its usage.</summary>
    /// <param name="request">System instruction, messages, tools and temperature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model reply.</returns>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken);
}
