using Microsoft.Extensions.Options;
using TutorFlow.Api.Endpoints;
using TutorFlow.Application;
using TutorFlow.Infrastructure;

// R-DEP-02: all configuration comes from environment variables (part of IConfiguration by default).
var builder = WebApplication.CreateBuilder(args);

// The port is configurable via ASPNETCORE_URLS / ASPNETCORE_HTTP_PORTS; default 8080 (R-DEP-01).
if (string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"])
    && string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_HTTP_PORTS"])
    && string.IsNullOrEmpty(builder.Configuration["HTTP_PORTS"])
    && string.IsNullOrEmpty(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls("http://0.0.0.0:8080");
}

// Each layer registers its own services (AGENTS 5).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

try
{
    // Resolve eagerly so a missing LLM_API_KEY stops startup with a clear message (R-DEP-02).
    _ = app.Services.GetRequiredService<IOptions<LlmOptions>>().Value;
}
catch (OptionsValidationException ex)
{
    Console.Error.WriteLine($"Startup aborted: {string.Join("; ", ex.Failures)}");
    return 1;
}

app.MapHealth();
app.MapAssistant();
app.Run();
return 0;

/// <summary>Entry point marker so integration tests can use <c>WebApplicationFactory</c>.</summary>
public partial class Program;
