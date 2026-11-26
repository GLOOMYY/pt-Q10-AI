using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using TutorFlow.Application.Ports.Llm;
using TutorFlow.Infrastructure.Llm;

namespace TutorFlow.Infrastructure.Tests.Llm;

/// <summary>AddLlm wires the real adapter (the fake LLM only exists in tests).</summary>
public sealed class LlmRegistrationTests
{
    [Fact]
    public void AddLlm_RegistersTheGeminiAdapterWithTheConfiguredTimeoutAsBackstop()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [LlmOptions.ApiKeyVariable] = "k" })
            .Build();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddLlm(config)
            .BuildServiceProvider();

        Assert.IsType<GeminiLlmClient>(provider.GetRequiredService<ILlmClient>());
        Assert.Equal(TimeSpan.FromSeconds(30), provider.GetRequiredService<IOptions<LlmOptions>>().Value.Timeout);
    }
}
