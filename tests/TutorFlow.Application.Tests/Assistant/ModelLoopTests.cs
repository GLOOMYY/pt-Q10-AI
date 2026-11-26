using TutorFlow.Application.Assistant;
using TutorFlow.Application.Assistant.Answering;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Ports.Llm;
using TutorFlow.Application.Tests.Support;
using TutorFlow.Testing.Llm;
using Xunit;

namespace TutorFlow.Application.Tests.Assistant;

/// <summary>
/// The LLM loop: <c>rechazar</c> (R-ARQ-04, D-29, D-32), dependency failures (503, API-02, CL-13, CL-23), tokens and
/// cost (R-RES-05), temperature 0 (R-QA-03), no memory (R-RES-07) and citation validation (R-ARQ-03, CL-18).
/// </summary>
public class ModelLoopTests
{
    // R-ARQ-04, D-29: `rechazar` first → 403 with the model's reason; the call is counted; no data consulted.
    [Theory]
    [InlineData(AuditReasons.ThirdPartyData)]
    [InlineData(AuditReasons.InjectionAttempt)]
    [InlineData(AuditReasons.SystemExtractionAttempt)]
    public async Task Reject_is_403_with_the_tokens_spent(string reason)
    {
        var h = new AssistantHarness();
        h.Llm.Enqueue(FakeLlmReply.Reject(reason).WithTokens(200, 10, 5));

        var answer = await h.AskStudentAsync("¿Cuántos estudiantes perdieron Cálculo Integral?");

        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.Equal(AssistantAnswer.ForbiddenText, answer.Text);
        Assert.Equal([AssistantAnswer.ForbiddenCitation], answer.Citations);
        Assert.False(answer.UsedStudentData);
        Assert.Equal(reason, answer.Reason);
        Assert.Equal(215, answer.Tokens);
        Assert.Equal(0.000073m, answer.CostUsd); // (200 × 0.25 + 15 × 1.50) / 1e6 = 0.0000725 → 0.000073
        Assert.Equal([AssistantTools.Reject], h.Audit.Tools);
        Assert.Empty(h.Audit.StudentsConsulted);
        Assert.Empty(h.StudentDataCalls);
    }

    [Fact]
    public async Task Reject_with_an_unknown_reason_uses_the_catalog()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueReject("porque sí");

        var answer = await h.AskStudentAsync("¿Quién es mi coordinador?");

        Assert.Equal(AuditReasons.ThirdPartyData, answer.Reason);
    }

    // D-32, R-LOG-04: if the model consulted data before rejecting, the log keeps what was really consulted.
    [Fact]
    public async Task Reject_after_a_data_tool_keeps_the_real_consultations()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueReject(AuditReasons.ThirdPartyData);

        var answer = await h.AskStudentAsync("Compara mi nota con la del curso");

        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.False(answer.UsedStudentData);
        Assert.Equal(["EST-001"], h.Audit.StudentsConsulted);
        Assert.Equal([AssistantTools.AcademicStatus, AssistantTools.Reject], h.Audit.Tools);
        Assert.Equal(2 * (FakeLlmReply.DefaultPromptTokens + FakeLlmReply.DefaultCandidatesTokens), answer.Tokens);
    }

    // CL-23: quota exhausted → 503; no answer is invented without the model.
    [Fact]
    public async Task Llm_quota_exhausted_is_503()
    {
        var h = new AssistantHarness();
        h.Llm.SimulateQuotaExhausted();

        var answer = await h.AskStudentAsync("¿Cuál es la nota mínima para aprobar una asignatura?");

        Assert.Equal(AssistantOutcome.Unavailable, answer.Outcome);
        Assert.Equal(AssistantAnswer.UnavailableText, answer.Text);
        Assert.Empty(answer.Citations);
        Assert.False(answer.UsedStudentData);
        Assert.Equal(0, answer.Tokens);
        Assert.Equal(0m, answer.CostUsd);
        Assert.Equal(AuditReasons.DependencyUnavailable, answer.Reason);
    }

    // CL-13, API-02: the API going down while a tool reads the grades → 503 with the tokens already spent; no grade
    // reaches the answer.
    [Fact]
    public async Task Api_down_during_a_tool_is_503_and_no_grade_is_invented()
    {
        var h = new AssistantHarness();
        h.Llm.Respond(_ =>
        {
            h.Api.Stop();
            return FakeLlmReply.FromToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus));
        });

        var answer = await h.AskStudentAsync("¿Cuál es mi promedio?");

        Assert.Equal(AssistantOutcome.Unavailable, answer.Outcome);
        Assert.Equal(AssistantAnswer.UnavailableText, answer.Text);
        Assert.False(answer.UsedStudentData);
        Assert.Equal(FakeLlmReply.DefaultPromptTokens + FakeLlmReply.DefaultCandidatesTokens, answer.Tokens);
        Assert.Equal(1, h.Llm.CallCount);
    }

    [Fact]
    public async Task Loop_without_a_final_answer_is_503_after_the_limit()
    {
        var h = new AssistantHarness();
        h.Options.MaxLlmCalls = 3;
        h.Llm.Respond(_ => FakeLlmReply.FromToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "beca" })));

        var answer = await h.AskStudentAsync("¿Qué requisitos tiene la beca?");

        Assert.Equal(AssistantOutcome.Unavailable, answer.Outcome);
        Assert.Equal(3, h.Llm.CallCount);
        Assert.Equal(3 * (FakeLlmReply.DefaultPromptTokens + FakeLlmReply.DefaultCandidatesTokens), answer.Tokens);
    }

    [Fact]
    public async Task Empty_final_answer_is_503()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText("   ");

        var answer = await h.AskStudentAsync("¿Qué es una habilitación?");

        Assert.Equal(AssistantOutcome.Unavailable, answer.Outcome);
    }

    // R-QA-03, R-RES-07, R-ARQ-01: temperature 0, a single user message, tools and structured output on every call.
    [Fact]
    public async Task Every_call_has_temperature_zero_and_no_history()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = "habilitación" }))
            .EnqueueText(AssistantHarness.Final("Respuesta.", ("reglamento_academico_2025.md", "Artículo 6")));

        await h.AskStudentAsync("¿Y en Bases de Datos?");

        Assert.All(h.Llm.Requests, r =>
        {
            Assert.Equal(0, r.Temperature);
            Assert.NotNull(r.ResponseSchema);
            Assert.Contains(r.Tools, t => t.Name == AssistantTools.Reject);
            Assert.Single(r.Messages, m => m.Role == LlmRole.User);
            Assert.Equal("¿Y en Bases de Datos?", r.Messages[0].Text);
        });
        Assert.Equal(3, h.Llm.Requests[1].Messages.Count);
    }

    // CL-18: an answer that depends on a non-existent article becomes "no information" with no citations.
    [Fact]
    public async Task Answer_citing_only_a_nonexistent_article_has_no_information()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final(
            "El artículo 8 dice que puedes presentar dos habilitaciones.",
            ("reglamento_academico_2025.md", "Artículo 8")));

        var answer = await h.AskStudentAsync("¿Qué dice el artículo 8?");

        Assert.Equal(AssistantOutcome.Answered, answer.Outcome);
        Assert.Equal(CitationValidator.NoInformationText, answer.Text);
        Assert.Empty(answer.Citations);
    }

    // R-RES-01, R-FUE-01, D-07: invalid citations are discarded, duplicates removed, order kept.
    [Fact]
    public async Task Invalid_and_duplicate_citations_are_removed()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final(
            "Se aprueba con 3.0 y la habilitación aplica desde 2.0.",
            ("reglamento_academico_2025.md", "Artículo 5"),
            ("reglamento_academico_2023.md", "Artículo 5"),
            ("reglamento_academico_2025.md", "Artículo 6"),
            ("reglamento_academico_2025.md", "Artículo 5"),
            ("bienestar_y_becas.md", "Sección 3"),
            ("reglamento_academico_2025.md", "Art. 6")));

        var answer = await h.AskStudentAsync("¿Con cuánto se aprueba?");

        Assert.Equal("Se aprueba con 3.0 y la habilitación aplica desde 2.0.", answer.Text);
        Assert.Equal(
            [
                new Citation("reglamento_academico_2025.md", "Artículo 5"),
                new Citation("reglamento_academico_2025.md", "Artículo 6"),
            ],
            answer.Citations);
    }

    // R-RES-02, R-FUE-02: data citations are only valid for data really used, with the period of GET /salud.
    [Fact]
    public async Task Data_citations_without_data_or_with_wrong_format_are_discarded()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final(
            "La nota mínima es 3.0.",
            ("reglamento_academico_2025.md", "Artículo 5"),
            ("Datos del estudiante", "Notas · periodo 2026-2")));

        var answer = await h.AskStudentAsync("¿Cuál es la nota mínima?");

        Assert.Equal([new Citation("reglamento_academico_2025.md", "Artículo 5")], answer.Citations);
        Assert.False(answer.UsedStudentData);
    }

    [Fact]
    public async Task Student_cannot_cite_program_data_and_gets_the_student_data_citations()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueToolCalls(FakeLlmReply.Call(AssistantTools.AcademicStatus))
            .EnqueueText(AssistantHarness.Final(
                "Tienes 2 faltas en Bases de Datos.",
                ("Datos del programa", "ING-SIS · periodo 2026-2"),
                ("Datos del estudiante", "Asistencia · periodo 2026-1")));

        var answer = await h.AskStudentAsync("¿Cuántas faltas tengo en Bases de Datos?");

        Assert.Equal(
            [
                new Citation("Datos del estudiante", "Notas · periodo 2026-2"),
                new Citation("Datos del estudiante", "Asistencia · periodo 2026-2"),
            ],
            answer.Citations);
    }

    // R-RES-03: "no information" never carries a filler citation.
    [Fact]
    public async Task No_information_answer_has_no_citations()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText(AssistantHarness.Final(
            "No encuentro esa información en los documentos disponibles.",
            ("reglamento_academico_2025.md", "Artículo 1")));

        var answer = await h.AskStudentAsync("¿Qué dice el reglamento sobre el uso de parqueaderos?");

        Assert.Empty(answer.Citations);
        Assert.False(answer.UsedStudentData);
    }

    [Fact]
    public async Task Plain_text_final_answer_is_accepted_without_citations()
    {
        var h = new AssistantHarness();
        h.Llm.EnqueueText("Formula la pregunta completa, por favor.");

        var answer = await h.AskStudentAsync("¿Y en Bases de Datos?");

        Assert.Equal("Formula la pregunta completa, por favor.", answer.Text);
        Assert.Empty(answer.Citations);
    }
}
