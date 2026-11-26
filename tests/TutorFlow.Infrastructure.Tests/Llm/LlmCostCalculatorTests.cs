using Xunit;
using TutorFlow.Infrastructure.Llm;

namespace TutorFlow.Infrastructure.Tests.Llm;

/// <summary>R-RES-05: tokens and cost of one call.</summary>
public sealed class LlmCostCalculatorTests
{
    [Fact]
    public void Compute_BillsThinkingTokensAsOutputWithoutRounding()
    {
        var usage = LlmCostCalculator.Compute(1234, 100, 50, 1384, 0.25m, 1.50m);

        // (1234 * 0.25 + 150 * 1.50) / 1e6 = 0.0005335: not rounded, the use case rounds the total to 6 decimals.
        Assert.Equal(0.0005335m, usage.CostUsd);
        Assert.Equal(1384, usage.TotalTokens);
    }

    [Fact]
    public void Compute_ZeroTokensCostNothing() =>
        Assert.Equal(0m, LlmCostCalculator.Compute(0, 0, 0, 0, 0.25m, 1.50m).CostUsd);

    [Fact]
    public void Compute_UsesTheConfiguredPrices() =>
        Assert.Equal(3m, LlmCostCalculator.Compute(1_000_000, 1_000_000, 0, 2_000_000, 1m, 2m).CostUsd);

    [Fact]
    public void Compute_RejectsNegativePrices() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LlmCostCalculator.Compute(1, 1, 0, 2, -0.1m, 1m));
}
