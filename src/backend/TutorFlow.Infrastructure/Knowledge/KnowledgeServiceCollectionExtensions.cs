using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TutorFlow.Application.Ports.Knowledge;

namespace TutorFlow.Infrastructure.Knowledge;

/// <summary>Registration of the knowledge adapter.</summary>
public static class KnowledgeServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IKnowledgeBase"/> as a singleton. The corpus is read from <c>KNOWLEDGE_CORPUS_PATH</c> or
    /// the default locations; a missing or invalid corpus makes the host fail at start-up with a clear message (a hosted service loads it).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration (environment variables included).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddKnowledge(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<KnowledgeOptions>()
            .Configure(o => o.CorpusPath = configuration[KnowledgeOptions.CorpusPathVariable]);
        services.AddSingleton<IKnowledgeBase>(sp =>
            KnowledgeBase.FromDirectory(sp.GetRequiredService<IOptions<KnowledgeOptions>>().Value.CorpusPath));
        services.AddHostedService<KnowledgeBaseStartupCheck>();
        return services;
    }
}

/// <summary>Loads the knowledge base when the host starts, so a missing corpus fails fast and not on the first question.</summary>
internal sealed class KnowledgeBaseStartupCheck : IHostedService
{
    private readonly IServiceProvider _services;

    public KnowledgeBaseStartupCheck(IServiceProvider services) => _services = services;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = _services.GetRequiredService<IKnowledgeBase>(); // Throws with the loader message if the corpus is missing.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
