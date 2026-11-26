using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using TutorFlow.Api.Endpoints;
using TutorFlow.Application;
using Xunit;

namespace TutorFlow.Api.Tests;

/// <summary>
/// Contract of <c>POST /api/asistente/preguntar</c> (SPEC §5, §5.0): the ESC-ENT scenarios that depend only on the
/// contract (400) and on the identity format (403). The host composes AddApplication() and MapAssistant() directly,
/// so it does not depend on the composition in Program.cs.
/// </summary>
public sealed class AssistantEndpointContractTests : IAsyncLifetime
{
    private const string Route = "/api/asistente/preguntar";
    private const string InvalidRequestText = "La solicitud no es válida.";
    private const string ForbiddenText = "No estás autorizado a obtener esa información.";

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddApplication();
        _app = builder.Build();
        _app.MapHealth();
        _app.MapAssistant();
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    // escenarios/README.md §3.3
    private static string T1000()
    {
        var text = new StringBuilder("¿Cuál es la nota mínima para aprobar una asignatura?");
        while (text.Length < 1000)
        {
            text.Append(" Gracias.");
        }

        return text.ToString(0, 1000);
    }

    private static string Body(string question, string role = "estudiante", string id = "EST-001") =>
        JsonSerializer.Serialize(new { pregunta = question, usuario = new { rol = role, id } });

    private async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(string rawBody)
    {
        using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(new Uri(Route, UriKind.Relative), content);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return (response.StatusCode, document.RootElement.Clone());
    }

    private static void AssertErrorShape(JsonElement body, string text)
    {
        Assert.Equal(text, body.GetProperty("respuesta").GetString());
        Assert.False(body.GetProperty("usoDatosEstudiante").GetBoolean());
        Assert.Equal(0, body.GetProperty("tokens").GetInt32());
        Assert.Equal(0m, body.GetProperty("costoUsd").GetDecimal());
    }

    private static void AssertBadRequest(HttpStatusCode status, JsonElement body)
    {
        Assert.Equal(HttpStatusCode.BadRequest, status);
        AssertErrorShape(body, InvalidRequestText);
        Assert.Equal(0, body.GetProperty("citas").GetArrayLength());
    }

    private static void AssertForbidden(HttpStatusCode status, JsonElement body)
    {
        Assert.Equal(HttpStatusCode.Forbidden, status);
        AssertErrorShape(body, ForbiddenText);
        var citation = Assert.Single(body.GetProperty("citas").EnumerateArray());
        Assert.Equal("reglamento_academico_2025.md", citation.GetProperty("documento").GetString());
        Assert.Equal("Artículo 22", citation.GetProperty("seccion").GetString());
    }

    public static TheoryData<string, string> BadRequestBodies => new()
    {
        { "ESC-ENT-01", Body("") },
        { "ESC-ENT-02", Body("     ") },
        { "ESC-ENT-04", Body(T1000() + "X") },
        { "ESC-ENT-07", "{\"pregunta\": \"hola\", \"usuario\": " },
        { "ESC-ENT-08", "{\"usuario\": {\"rol\": \"estudiante\", \"id\": \"EST-001\"}}" },
        { "ESC-ENT-09", "{\"pregunta\": \"¿Cuál es mi promedio?\"}" },
        { "ESC-ENT-10", Body("¿Cuál es mi promedio?", role: "profesor") },
        { "ESC-ENT-11", "{\"pregunta\": 123, \"usuario\": {\"rol\": \"estudiante\", \"id\": \"EST-001\"}}" },
        { "ESC-ENT-12", "{}" },
        { "empty body", string.Empty },
        { "JSON null", "null" },
    };

    [Theory]
    [MemberData(nameof(BadRequestBodies))]
    public async Task Malformed_or_invalid_body_returns_400_with_contract_shape(string scenario, string rawBody)
    {
        Assert.NotNull(scenario);
        var (status, body) = await PostAsync(rawBody);
        AssertBadRequest(status, body);
    }

    public static TheoryData<string, string> ForbiddenBodies => new()
    {
        { "ESC-ENT-14", Body("¿Cuál es mi promedio?", id: "COORD-01") },
        { "ESC-ENT-15", Body("¿Cuál es mi promedio?", id: "EST-%E0%A4%A") },
        { "ESC-ENT-16", Body("¿Cuál es mi promedio?", id: "../programas") },
        { "ESC-ENT-17", Body("¿Cuál es mi promedio?", id: "") },
    };

    // R-SEG-04, D-26: an invalid identity format is a 403 decided before any call to the API.
    [Theory]
    [MemberData(nameof(ForbiddenBodies))]
    public async Task Invalid_identity_format_returns_403_with_article_22(string scenario, string rawBody)
    {
        Assert.NotNull(scenario);
        var (status, body) = await PostAsync(rawBody);
        AssertForbidden(status, body);
    }

    // ESC-ENT-15 "despues": the system keeps answering after a dangerous id.
    [Fact]
    public async Task Health_still_answers_after_a_badly_encoded_id()
    {
        var (status, _) = await PostAsync(Body("¿Cuál es mi promedio?", id: "EST-%E0%A4%A"));
        Assert.Equal(HttpStatusCode.Forbidden, status);

        using var health = await _client.GetAsync(new Uri("/api/salud", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    // ESC-ENT-03, ESC-ENT-05, ESC-ENT-06, ESC-ENT-18: these bodies follow the contract and a well-formed identity,
    // so they are never a 400 nor a format 403 (their 200 depends on the F2 pipeline).
    public static TheoryData<string, string> ValidBodies => new()
    {
        { "ESC-ENT-03", Body(T1000()) },
        { "ESC-ENT-05", Body("   " + T1000() + "   ") },
        { "ESC-ENT-06", Body(string.Concat(Enumerable.Repeat("ñáéíóú", 167))[..1000]) },
        { "ESC-ENT-18", Body("¿Cuál es mi promedio ponderado?", id: " est-001 ") },
    };

    [Theory]
    [MemberData(nameof(ValidBodies))]
    public async Task Valid_contract_body_is_not_rejected_by_validation(string scenario, string rawBody)
    {
        Assert.NotNull(scenario);
        var (status, _) = await PostAsync(rawBody);

        Assert.NotEqual(HttpStatusCode.BadRequest, status);
        Assert.NotEqual(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task Response_uses_the_exact_contract_field_names()
    {
        var (_, body) = await PostAsync("{}");
        var names = body.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(["respuesta", "citas", "usoDatosEstudiante", "tokens", "costoUsd"], names);
    }
}
