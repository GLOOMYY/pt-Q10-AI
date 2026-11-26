using TutorFlow.Application.Ports.Llm;

namespace TutorFlow.Infrastructure.Llm;

/// <summary>Token usage and cost of one LLM call (R-RES-05).</summary>
public static class LlmCostCalculator
{
    private const decimal TokensPerMillion = 1_000_000m;

    /// <summary>
    /// Computes the usage of one call. The cost is <b>not rounded</b> (the port contract): the use case sums the calls
    /// and rounds the total to 6 decimals. Reasoning tokens are billed as output (R-RES-05).
    /// </summary>
    /// <param name="promptTokens"><c>promptTokenCount</c>.</param>
    /// <param name="candidatesTokens"><c>candidatesTokenCount</c>.</param>
    /// <param name="thoughtsTokens"><c>thoughtsTokenCount</c>.</param>
    /// <param name="totalTokens"><c>totalTokenCount</c>, as reported by the provider.</param>
    /// <param name="inputPriceUsdPerMTok">USD per million input tokens.</param>
    /// <param name="outputPriceUsdPerMTok">USD per million output tokens.</param>
    /// <returns>The usage with its cost.</returns>
    public static LlmUsage Compute(
        int promptTokens,
        int candidatesTokens,
        int thoughtsTokens,
        int totalTokens,
        decimal inputPriceUsdPerMTok,
        decimal outputPriceUsdPerMTok)
    {
        if (promptTokens < 0 || candidatesTokens < 0 || thoughtsTokens < 0 || totalTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(promptTokens), "Token counts must not be negative.");
        }

        if (inputPriceUsdPerMTok < 0m || outputPriceUsdPerMTok < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(inputPriceUsdPerMTok), "Prices must not be negative.");
        }

        var cost = ((promptTokens * inputPriceUsdPerMTok)
            + ((candidatesTokens + thoughtsTokens) * outputPriceUsdPerMTok)) / TokensPerMillion;
        return new LlmUsage(promptTokens, candidatesTokens, thoughtsTokens, totalTokens, cost);
    }
}
