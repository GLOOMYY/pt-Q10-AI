using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Ports.Llm;
using TutorFlow.Testing.AcademicApi;
using TutorFlow.Testing.Audit;
using TutorFlow.Testing.Llm;

namespace TutorFlow.Integration.Tests.Support;

/// <summary>
/// The real backend (<c>Program.cs</c>, with its middleware, defenses, use case, tools and knowledge base) running in
/// process, with only the three external ports replaced by the doubles of <c>tests/TutorFlow.Testing</c>
/// (AGENTS section 8): the simulated academic API, the LLM and the audit log.
/// </summary>
public sealed class IntegrationHost : WebApplicationFactory<Program>
{
    /// <summary>Header that carries the correlation id (R-LOG-09).</summary>
    public const string CorrelationHeader = "X-Correlation-Id";

    /// <summary>Double of the simulated API built from <c>material/mock-api/datos.json</c>.</summary>
    public FakeAcademicApi Api { get; } = FakeAcademicApi.FromDefaultData();

    /// <summary>The fake LLM: scripted, never touches the network.</summary>
    public FakeLlmClient Llm { get; } = new();

    /// <summary>In-memory audit log: a record per request, found by correlation id.</summary>
    public InMemoryAuditLog Audit { get; } = new();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // R-DEP-02: the key is mandatory for the real adapter; here it is a fake value because the LLM is a double.
        builder.UseSetting("LLM_API_KEY", "integration-tests-fake-key");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAcademicApi>();
            services.AddSingleton<IAcademicApi>(Api);
            services.RemoveAll<ILlmClient>();
            services.AddSingleton<ILlmClient>(Llm);
            services.RemoveAll<IAuditLog>();
            services.AddSingleton<IAuditLog>(Audit);
        });
    }
}
