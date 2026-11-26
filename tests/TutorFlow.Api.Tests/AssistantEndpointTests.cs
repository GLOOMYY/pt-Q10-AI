using System.Text;
using System.Text.Json;
using TutorFlow.Api.Tests.Support;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Testing.Llm;
using Xunit;

namespace TutorFlow.Api.Tests;

/// <summary>
/// <c>POST /api/asistente/preguntar</c> end to end with the fake LLM and the double of the simulated API: the parts of
/// ESC-ENT, ESC-BAN, ESC-EST, ESC-COO and ESC-SIM that tests/ checks (escenarios/README §4.4: status, fixed texts,
/// citations of 400/403/503, usoDatosEstudiante, tokens/cost = 0 and the consulted students of the log).
/// </summary>
public sealed class AssistantEndpointTests
{
    private const string ForbiddenText = "No estás autorizado a obtener esa información.";
    private const string UnavailableText = "Los datos no están disponibles en este momento. Intenta más tarde.";

    private static string T1000()
    {
        var text = new StringBuilder("¿Cuál es la nota mínima para aprobar una asignatura?");
        while (text.Length < 1000)
        {
            text.Append(" Gracias.");
        }

        return text.ToString(0, 1000);
    }

    private static void AssertZeroCost(JsonElement body)
    {
        Assert.False(body.GetProperty("usoDatosEstudiante").GetBoolean());
        Assert.Equal(0, body.GetProperty("tokens").GetInt32());
        Assert.Equal(0m, body.GetProperty("costoUsd").GetDecimal());
    }

    private static void AssertArticle22(JsonElement body)
    {
        Assert.Equal(ForbiddenText, body.GetProperty("respuesta").GetString());
        var citation = Assert.Single(body.GetProperty("citas").EnumerateArray());
        Assert.Equal("reglamento_academico_2025.md", citation.GetProperty("documento").GetString());
        Assert.Equal("Artículo 22", citation.GetProperty("seccion").GetString());
    }

    // ESC-ENT-03, ESC-ENT-05, ESC-ENT-06: valid lengths are answered (200).
    [Theory]
    [InlineData("ESC-ENT-03")]
    [InlineData("ESC-ENT-05")]
    [InlineData("ESC-ENT-06")]
    public async Task Valid_lengths_are_answered(string scenario)
    {
        var question = scenario switch
        {
            "ESC-ENT-03" => T1000(),
            "ESC-ENT-05" => "   " + T1000() + "   ",
            _ => string.Concat(Enumerable.Repeat("ñáéíóú", 167))[..1000],
        };
        await using var host = await AssistantTestHost.StartAsync();
        host.Llm.EnqueueText(AssistantTestHost.Final("La nota mínima es 3.0.", ("reglamento_academico_2025.md", "Artículo 5")));

        var (status, body) = await host.PostAsync(AssistantTestHost.Body(question));

        Assert.Equal(200, status);
        Assert.False(body.GetProperty("usoDatosEstudiante").GetBoolean());

        // D-27: the question is recorded as received, not trimmed.
        Assert.Equal(question, Assert.Single(host.Audits).Question);
    }

    // ESC-ENT-18, API-05: " est-001 " is EST-001; its own average uses student data.
    [Fact]
    public async Task Normalized_id_answers_with_own_data()
    {
        await using var host = await AssistantTestHost.StartAsync();
        host.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantTestHost.Final("Tu promedio es 4.39.", ("Datos del estudiante", "Notas · periodo 2026-2")));

        var (status, body) = await host.PostAsync(AssistantTestHost.Body("¿Cuál es mi promedio ponderado?", id: " est-001 "));

        Assert.Equal(200, status);
        Assert.True(body.GetProperty("usoDatosEstudiante").GetBoolean());
        var audit = Assert.Single(host.Audits);
        Assert.Equal(["EST-001"], audit.StudentsConsulted);
        Assert.Equal(" est-001 ", audit.User!.Id);
        Assert.Equal("estudiante", audit.User.Role);
        Assert.Equal(1, audit.CompleteCalls);
        Assert.Contains(body.GetProperty("citas").EnumerateArray(), c => c.GetProperty("seccion").GetString() == "Notas · periodo 2026-2");
    }

    // ESC-ENT-13: an id that does not exist is the standard 403, with no LLM call.
    [Theory]
    [InlineData("estudiante", "EST-999")]
    [InlineData("coordinador", "COORD-09")]
    public async Task Nonexistent_id_is_403(string role, string id)
    {
        await using var host = await AssistantTestHost.StartAsync();

        var (status, body) = await host.PostAsync(AssistantTestHost.Body("¿Cuál es mi promedio?", role, id));

        Assert.Equal(403, status);
        AssertArticle22(body);
        AssertZeroCost(body);
        Assert.Empty(Assert.Single(host.Audits).StudentsConsulted);
        Assert.Equal(0, host.Llm.CallCount);
    }

    // CL-13 (api-simulada-detenida): 503 with the standard body; no grades are invented.
    [Fact]
    public async Task Api_stopped_is_503()
    {
        await using var host = await AssistantTestHost.StartAsync();
        host.Api.Stop();

        var (status, body) = await host.PostAsync(AssistantTestHost.Body("¿Cuál es mi promedio?"));

        Assert.Equal(503, status);
        Assert.Equal(UnavailableText, body.GetProperty("respuesta").GetString());
        Assert.Equal(0, body.GetProperty("citas").GetArrayLength());
        AssertZeroCost(body);
    }

    // CL-23 (llm-cuota-agotada): 503 with the standard body.
    [Fact]
    public async Task Llm_quota_exhausted_is_503()
    {
        await using var host = await AssistantTestHost.StartAsync();
        host.Llm.SimulateQuotaExhausted();

        var (status, body) = await host.PostAsync(AssistantTestHost.Body("¿Cuál es la nota mínima para aprobar una asignatura?"));

        Assert.Equal(503, status);
        Assert.Equal(UnavailableText, body.GetProperty("respuesta").GetString());
        Assert.Equal(0, body.GetProperty("citas").GetArrayLength());
        AssertZeroCost(body);
    }

    // ESC-EST-23..25, R-ARQ-04: `rechazar` is a 403 with the standard body; tokens count the call made.
    [Fact]
    public async Task Reject_is_403_with_the_standard_body()
    {
        await using var host = await AssistantTestHost.StartAsync();
        host.Llm.EnqueueReject("datos de terceros");

        var (status, body) = await host.PostAsync(AssistantTestHost.Body("¿Cuántos estudiantes perdieron Cálculo Integral?"));

        Assert.Equal(403, status);
        AssertArticle22(body);
        Assert.False(body.GetProperty("usoDatosEstudiante").GetBoolean());
        Assert.Equal(FakeLlmReply.DefaultPromptTokens + FakeLlmReply.DefaultCandidatesTokens, body.GetProperty("tokens").GetInt32());
        var audit = Assert.Single(host.Audits);
        Assert.Empty(audit.StudentsConsulted);
        Assert.Equal("datos de terceros", audit.Answer!.Reason);
    }

    // CL-10, R-SEG-03: a coordinator asking the tools for a student of another program is a 403.
    [Fact]
    public async Task Coordinator_tool_for_another_program_is_403()
    {
        await using var host = await AssistantTestHost.StartAsync();
        host.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus, new { estudianteId = "EST-005" }));

        var (status, body) = await host.PostAsync(AssistantTestHost.Body("¿Qué notas tiene EST-005?", "coordinador", "COORD-01"));

        Assert.Equal(403, status);
        AssertArticle22(body);
        Assert.Empty(Assert.Single(host.Audits).StudentsConsulted);
        Assert.DoesNotContain(host.Api.Calls, c => c.StartsWith("/estudiantes/", StringComparison.Ordinal));
    }

    // ESC-BAN-06: COORD-02 lost courses → the three ADM students are consulted, usoDatosEstudiante true.
    [Fact]
    public async Task Coordinator_program_question_consults_its_students()
    {
        await using var host = await AssistantTestHost.StartAsync();
        host.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.ProgramSummary))
            .EnqueueText(AssistantTestHost.Final(
                "Valentina Castro tiene Microeconomía y Estadística perdidas.",
                ("Datos del programa", "ADM · periodo 2026-2"),
                ("reglamento_academico_2025.md", "Artículo 5")));

        var (status, body) = await host.PostAsync(AssistantTestHost.Body(
            "¿Qué estudiantes de mi programa tienen alguna asignatura perdida?", "coordinador", "COORD-02"));

        Assert.Equal(200, status);
        Assert.True(body.GetProperty("usoDatosEstudiante").GetBoolean());
        Assert.Equal(["EST-004", "EST-005", "EST-006"], Assert.Single(host.Audits).StudentsConsulted);
    }

    // ESC-BAN-09, CL-14: no information → citations [] and no student data.
    [Fact]
    public async Task Out_of_corpus_question_has_no_citations()
    {
        await using var host = await AssistantTestHost.StartAsync();
        host.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "parqueaderos" }))
            .EnqueueText(AssistantTestHost.Final("No encuentro esa información en los documentos disponibles."));

        var (status, body) = await host.PostAsync(AssistantTestHost.Body("¿Qué dice el reglamento sobre el uso de parqueaderos?"));

        Assert.Equal(200, status);
        Assert.Equal(0, body.GetProperty("citas").GetArrayLength());
        Assert.False(body.GetProperty("usoDatosEstudiante").GetBoolean());
        Assert.Empty(Assert.Single(host.Audits).StudentsConsulted);
    }

    // A malformed body is still recorded (nothing could be read) and completed once.
    [Fact]
    public async Task Malformed_body_is_recorded_and_completed()
    {
        await using var host = await AssistantTestHost.StartAsync();

        var (status, _) = await host.PostAsync("{\"pregunta\": \"hola\", \"usuario\": ");

        Assert.Equal(400, status);
        var audit = Assert.Single(host.Audits);
        Assert.Null(audit.Question);
        Assert.Null(audit.User!.Role);
        Assert.Equal("solicitud inválida", audit.Answer!.Reason);
    }
}
