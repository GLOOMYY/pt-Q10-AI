using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TutorFlow.Infrastructure;
using Xunit;

namespace TutorFlow.Api.Tests;

public class HealthEndpointTests
{
    private static WebApplicationFactory<Program> Factory(string? apiKey) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseSetting("LLM_API_KEY", apiKey ?? string.Empty));

    [Fact]
    public async Task Health_returns_ok()
    {
        using var factory = Factory("test-key");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/salud", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(CancellationToken.None);
        Assert.Equal("ok", body!["estado"]);
    }

    // R-DEP-02: missing LLM_API_KEY must be reported as a clear validation failure.
    [Fact]
    public void Missing_llm_api_key_fails_options_validation()
    {
        using var factory = Factory(null);
        var ex = Assert.Throws<OptionsValidationException>(() =>
            factory.Services.GetRequiredService<IOptions<LlmOptions>>().Value.ToString());
        Assert.Contains("LLM_API_KEY", ex.Message);
    }
}
