using TutorFlow.Application.Ports.Llm;
using Xunit;

namespace TutorFlow.Application.Tests.Ports;

/// <summary>The single cost formula of one LLM call: R-RES-05 (DUDA-23).</summary>
public class LlmCostTests
{
    [Fact]
    public void Bills_prompt_as_input_and_candidates_plus_thoughts_as_output()
    {
        // (1000 × 0.30 + (200 + 50) × 2.50) / 1 000 000 = (300 + 625) / 1 000 000
        var cost = LlmCost.Compute(1000, 200, 50, 0.30m, 2.50m);

        Assert.Equal(0.000925m, cost);
    }

    [Fact]
    public void Does_not_round_the_cost()
    {
        // (1 × 0.30 + 1 × 2.50) / 1 000 000 = 0.0000028: more than 6 decimals, kept exactly.
        var cost = LlmCost.Compute(1, 1, 0, 0.30m, 2.50m);

        Assert.Equal(0.0000028m, cost);
    }

    [Fact]
    public void Is_zero_without_tokens()
    {
        Assert.Equal(0m, LlmCost.Compute(0, 0, 0, 0.30m, 2.50m));
    }

    [Fact]
    public void Does_not_overflow_with_large_output_counts()
    {
        var cost = LlmCost.Compute(0, int.MaxValue, int.MaxValue, 0m, 1m);

        Assert.Equal(2m * int.MaxValue / 1_000_000m, cost);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 0, "promptTokens")]
    [InlineData(0, -1, 0, 0, 0, "candidatesTokens")]
    [InlineData(0, 0, -1, 0, 0, "thoughtsTokens")]
    [InlineData(0, 0, 0, -1, 0, "inputPriceUsdPerMTok")]
    [InlineData(0, 0, 0, 0, -1, "outputPriceUsdPerMTok")]
    public void Rejects_negative_arguments_naming_the_parameter(
        int prompt, int candidates, int thoughts, int inputPrice, int outputPrice, string parameter)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => LlmCost.Compute(prompt, candidates, thoughts, inputPrice, outputPrice));

        Assert.Equal(parameter, error.ParamName);
    }
}
