using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TutorFlow.Application.Ports.Knowledge;

namespace TutorFlow.Infrastructure.Knowledge;

/// <summary>Registration of the knowledge adapter.</summary>
public static class KnowledgeServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IKnowledgeBase"/> as a singleton. The corpus is read from <c>KNOWLEDGE_CORPUS_PATH</c> or
    /// the default locations; a missing corpus makes the first resolution fail with a clear message.
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
        return services;
    }
}
