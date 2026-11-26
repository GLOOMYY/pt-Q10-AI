using TutorFlow.Application.Ports;
using TutorFlow.Application.Ports.Llm;

namespace TutorFlow.Application.Fallbacks;

/// <summary>
/// Default <see cref="ILlmClient"/> while no real adapter is registered: it is always unavailable, so a question that
/// reaches the model ends in the standard 503 and no answer is ever invented (SPEC §8, Prohibition 1). Registered with
/// <c>TryAdd</c> by <see cref="DependencyInjection.AddApplication"/>; the real adapter replaces it.
/// </summary>
public sealed class UnconfiguredLlmClient : ILlmClient
{
    /// <inheritdoc />
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException(ExternalDependency.Llm, "No LLM adapter is registered.");
}
