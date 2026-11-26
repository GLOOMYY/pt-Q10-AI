using Microsoft.Extensions.DependencyInjection;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Audit;

/// <summary>Registration of the audit services of the Application layer.</summary>
public static class AuditServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IRequestAuditContext"/> and <see cref="AuditTrailRecorder"/> as scoped: one instance per HTTP
    /// request, so nothing survives between questions. <c>Program.cs</c> (plataforma) invokes it next to
    /// <c>AddApplication()</c>; the <see cref="IAuditLog"/> adapter comes from <c>AddAuditLog()</c> in Infrastructure.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAudit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IRequestAuditContext, RequestAuditContext>();
        services.AddScoped<AuditTrailRecorder>();
        return services;
    }
}
