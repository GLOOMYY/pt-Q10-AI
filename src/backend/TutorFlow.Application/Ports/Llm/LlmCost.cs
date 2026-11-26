namespace TutorFlow.Application.Ports.Llm;

/// <summary>
/// The single cost formula of one LLM call (R-RES-05). Every <see cref="ILlmClient"/> adapter and test double fills
/// <see cref="LlmUsage.CostUsd"/> with this calculation so that the formula lives in one place (DUDA-23).
/// </summary>
public static class LlmCost
{
    /// <summary>Prices are expressed in USD per million tokens.</summary>
    private const decimal TokensPerMillion = 1_000_000m;

    /// <summary>
    /// Computes the cost of one call:
    /// <c>(promptTokenCount × input price + (candidatesTokenCount + thoughtsTokenCount) × output price) / 1 000 000</c>.
    /// Reasoning tokens are billed as output. The result is <b>not rounded</b>: the use case sums the calls of the
    /// request and rounds the total to 6 decimals (R-RES-05, D-06).
    /// </summary>
    /// <param name="promptTokens"><c>promptTokenCount</c>.</param>
    /// <param name="candidatesTokens"><c>candidatesTokenCount</c>.</param>
    /// <param name="thoughtsTokens"><c>thoughtsTokenCount</c>.</param>
    /// <param name="inputPriceUsdPerMTok">Configured price, in USD per million input tokens (R-DEP-02).</param>
    /// <param name="outputPriceUsdPerMTok">Configured price, in USD per million output tokens (R-DEP-02).</param>
    /// <returns>The exact cost in USD, without rounding.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Any argument is negative.</exception>
    public static decimal Compute(
        int promptTokens,
        int candidatesTokens,
        int thoughtsTokens,
        decimal inputPriceUsdPerMTok,
        decimal outputPriceUsdPerMTok)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(promptTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(candidatesTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(thoughtsTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(inputPriceUsdPerMTok);
        ArgumentOutOfRangeException.ThrowIfNegative(outputPriceUsdPerMTok);

        // Sum the output counters as decimal so that large counts cannot overflow int.
        var outputTokens = (decimal)candidatesTokens + thoughtsTokens;
        return ((promptTokens * inputPriceUsdPerMTok) + (outputTokens * outputPriceUsdPerMTok)) / TokensPerMillion;
    }
}
