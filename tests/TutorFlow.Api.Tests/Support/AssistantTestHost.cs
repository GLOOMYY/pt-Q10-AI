using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TutorFlow.Api.Endpoints;
using TutorFlow.Application;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Ports.Knowledge;
using TutorFlow.Application.Ports.Llm;
using TutorFlow.Infrastructure.Knowledge;
using TutorFlow.Testing.AcademicApi;
using TutorFlow.Testing.Llm;

namespace TutorFlow.Api.Tests.Support;

/// <summary>
/// In-process host of the assistant endpoint: <c>AddApplication()</c> and <c>MapAssistant()</c> with the double of the
/// simulated API built from <c>datos.json</c>, the fake LLM (AGENTS §8), the real knowledge base over
/// <c>material/reglamento</c> and a recording audit context. Scopes are validated on build, as in Development.
/// </summary>
public sealed class AssistantTestHost : IAsyncDisposable
{
    public const string Route = "/api/asistente/preguntar";

    private WebApplication _app = null!;

    private AssistantTestHost()
    {
    }

    public FakeAcademicApi Api { get; } = FakeAcademicApi.FromDefaultData();

    public FakeLlmClient Llm { get; } = new();

    /// <summary>Audit contexts created so far, one per request.</summary>
    public List<RecordingAuditContext> Audits { get; } = [];

    public HttpClient Client { get; private set; } = null!;

    public static async Task<AssistantTestHost> StartAsync(params IQuestionGuard[] guards)
    {
        var host = new AssistantTestHost();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateOnBuild = true;
            o.ValidateScopes = true;
        });
        builder.Services.AddApplication();
        builder.Services.AddSingleton<IAcademicApi>(host.Api);
        builder.Services.AddSingleton<ILlmClient>(host.Llm);
        builder.Services.AddSingleton<IKnowledgeBase>(_ => KnowledgeBase.FromDirectory(null));
        builder.Services.AddScoped<IRequestAuditContext>(_ =>
        {
            var audit = new RecordingAuditContext();
            lock (host.Audits)
            {
                host.Audits.Add(audit);
            }

            return audit;
        });
        foreach (var guard in guards)
        {
            builder.Services.AddSingleton(guard);
        }

        host._app = builder.Build();
        host._app.MapHealth();
        host._app.MapAssistant();
        await host._app.StartAsync();
        host.Client = host._app.GetTestClient();
        return host;
    }

    public static string Body(string question, string role = "estudiante", string id = "EST-001") =>
        JsonSerializer.Serialize(new { pregunta = question, usuario = new { rol = role, id } });

    public static string Final(string answer, params (string Document, string Section)[] citations) =>
        JsonSerializer.Serialize(new
        {
            respuesta = answer,
            citas = citations.Select(c => new { documento = c.Document, seccion = c.Section }),
        });

    public async Task<(int Status, JsonElement Body)> PostAsync(string rawBody)
    {
        using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync(new Uri(Route, UriKind.Relative), content);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return ((int)response.StatusCode, document.RootElement.Clone());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
