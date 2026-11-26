using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Infrastructure.Audit;

/// <summary>Registration of the audit log adapter.</summary>
public static class AuditLogServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AuditLogOptions"/> (default <c>var/auditoria/auditoria.jsonl</c>, optional override
    /// <c>AUDIT_LOG_PATH</c>) and <see cref="IAuditLog"/> as the singleton <see cref="FileAuditLog"/>.
    /// <c>AddInfrastructure()</c> (plataforma) invokes it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration (environment variables included).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAuditLog(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<AuditLogOptions>().Configure(o =>
        {
            var path = configuration[AuditLogOptions.FilePathKey];
            if (!string.IsNullOrWhiteSpace(path))
            {
                o.FilePath = path.Trim();
            }
        });
        services.AddSingleton<IAuditLog, FileAuditLog>();
        return services;
    }
}
