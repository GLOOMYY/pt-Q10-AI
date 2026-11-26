using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TutorFlow.Infrastructure.Llm;

namespace TutorFlow.Infrastructure.Tests.Llm;

/// <summary>R-DEP-02, R-QA-03, R-QA-04, R-RES-05 and DUDA-04: options fail clearly instead of defaulting silently.</summary>
public sealed class LlmOptionsTests
{
    private static IOptions<TutorFlow.Infrastructure.Llm.LlmOptions> Build(params (string Key, string? Value)[] env)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(env.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            .Build();
        var provider = new ServiceCollection().AddLlm(configuration).BuildServiceProvider();
        return provider.GetRequiredService<IOptions<TutorFlow.Infrastructure.Llm.LlmOptions>>();
    }

    [Fact]
    public void Defaults_FollowTheSpec()
    {
        var options = Build(("LLM_API_KEY", "fake-key")).Value;

        Assert.Equal("gemini-3.1-flash-lite", options.Model);
        Assert.Equal(0.25m, options.InputPriceUsdPerMTok);
        Assert.Equal(1.50m, options.OutputPriceUsdPerMTok);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
        Assert.Equal(0d, options.Temperature);
    }

    [Fact]
    public void Environment_OverridesModelAndPrices()
    {
        var options = Build(
            ("LLM_API_KEY", "k"),
            ("LLM_MODEL", "other"),
            ("LLM_PRECIO_ENTRADA_USD_MTOK", "0.5"),
            ("LLM_PRECIO_SALIDA_USD_MTOK", "2")).Value;

        Assert.Equal("other", options.Model);
        Assert.Equal(0.5m, options.InputPriceUsdPerMTok);
        Assert.Equal(2m, options.OutputPriceUsdPerMTok);
    }

    [Fact]
    public void MissingApiKey_FailsWithAClearMessage()
    {
        var ex = Assert.Throws<OptionsValidationException>(() => Build().Value);

        Assert.Contains("LLM_API_KEY", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("LLM_PRECIO_ENTRADA_USD_MTOK", "abc")]
    [InlineData("LLM_PRECIO_ENTRADA_USD_MTOK", "-1")]
    [InlineData("LLM_PRECIO_SALIDA_USD_MTOK", "abc")]
    [InlineData("LLM_PRECIO_SALIDA_USD_MTOK", "-0.01")]
    public void InvalidOrNegativePrice_FailsInsteadOfUsingTheDefault(string variable, string value)
    {
        var ex = Assert.Throws<OptionsValidationException>(() => Build(("LLM_API_KEY", "k"), (variable, value)).Value);

        Assert.Contains(variable, ex.Message, StringComparison.Ordinal);
    }
}
