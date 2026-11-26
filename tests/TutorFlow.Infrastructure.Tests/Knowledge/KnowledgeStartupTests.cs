using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using TutorFlow.Infrastructure.Knowledge;

namespace TutorFlow.Infrastructure.Tests.Knowledge;

/// <summary>A missing corpus must stop the host at start-up with a clear message (R-FUE-01).</summary>
public sealed class KnowledgeStartupTests
{
    private static ServiceProvider Build(string? corpusPath)
    {
        var values = new Dictionary<string, string?> { [KnowledgeOptions.CorpusPathVariable] = corpusPath };
        return new ServiceCollection()
            .AddKnowledge(new ConfigurationBuilder().AddInMemoryCollection(values).Build())
            .BuildServiceProvider();
    }

    [Fact]
    public async Task MissingCorpus_FailsWhenTheHostedServicesStart()
    {
        using var provider = Build(Path.Combine(Path.GetTempPath(), "no-such-corpus-dir"));
        var startup = provider.GetServices<IHostedService>().Single();

        await Assert.ThrowsAnyAsync<Exception>(() => startup.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DefaultCorpus_StartsFine()
    {
        using var provider = Build(null);
        var startup = provider.GetServices<IHostedService>().Single();

        await startup.StartAsync(CancellationToken.None);
    }
}
