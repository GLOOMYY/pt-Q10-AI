using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Audit;
using TutorFlow.Application.Fallbacks;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Ports.Llm;
using Xunit;

namespace TutorFlow.Api.Tests;

/// <summary>
/// Composition of the real <c>Program.cs</c> (Development: scopes and services validated on build): the fallback LLM is
/// replaced by the real adapter, the real audit context is registered, and the deterministic defenses reach the use case
/// through <see cref="DefenseGuard"/> (R-SEG-14, R-LOG-01).
/// </summary>
public class CompositionTests
{
    [Fact]
    public void Program_composes_the_real_adapters_and_the_defense_guard()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("LLM_API_KEY", "test-key");
            b.UseSetting("Assistant:MaxLlmCalls", "5");
        });
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        Assert.IsNotType<UnconfiguredLlmClient>(services.GetRequiredService<ILlmClient>());
        Assert.IsType<RequestAuditContext>(services.GetRequiredService<IRequestAuditContext>());
        Assert.Contains(services.GetServices<IQuestionGuard>(), g => g is DefenseGuard);
        Assert.Equal(5, services.GetRequiredService<IOptions<AssistantOptions>>().Value.MaxLlmCalls);
    }
}
