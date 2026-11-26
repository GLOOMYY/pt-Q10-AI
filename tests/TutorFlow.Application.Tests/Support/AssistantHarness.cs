using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TutorFlow.Application.Academic;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Assistant.Answering;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Audit;
using TutorFlow.Application.Authorization;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Ports.Llm;
using TutorFlow.Application.Security.Defenses;
using TutorFlow.Testing.AcademicApi;
using TutorFlow.Testing.Llm;

namespace TutorFlow.Application.Tests.Support;

/// <summary>
/// Composes the real use case with the in-process double of the simulated API, the fake LLM (AGENTS §8), the knowledge
/// base stub and the real per-request audit context of seguridad, exactly as <c>AddApplication()</c> wires it.
/// One harness = one request; <see cref="Log"/> is the audit record that request would write (R-LOG-01, R-LOG-08).
/// </summary>
public sealed class AssistantHarness
{
    private readonly List<IQuestionGuard> _guards;
    private AssistantAnswer? _answer;

    public AssistantHarness(params IQuestionGuard[] guards)
    {
        _guards = [.. guards];
        Audit.Start("test-correlation-id", DateTimeOffset.UtcNow);
    }

    public FakeAcademicApi Api { get; } = FakeAcademicApi.FromDefaultData();

    public FakeLlmClient Llm { get; } = new();

    public StubKnowledgeBase Knowledge { get; } = new();

    public RequestAuditContext Audit { get; } = new();

    public AssistantOptions Options { get; } = new();

    /// <summary>The audit record of the request, with the status of the last answer.</summary>
    public AuditRecord Log => Audit.BuildRecord(StatusOf(_answer), DateTimeOffset.UtcNow);

    /// <summary>
    /// Adds the real deterministic defenses of seguridad (<see cref="QuestionDefenseChain"/>) through the
    /// <see cref="DefenseGuard"/> adapter, as <c>AddApplication()</c> + <c>AddSecurityDefenses()</c> do.
    /// </summary>
    /// <returns>This harness.</returns>
    public AssistantHarness WithSecurityDefenses()
    {
        var chain = new QuestionDefenseChain(new InjectionDefense(), new SystemExtractionDefense(), new DeterministicBarrier(Api, Audit));
        _guards.Insert(0, new DefenseGuard([chain]));
        return this;
    }

    public async Task<AssistantAnswer> AskAsync(string question, string role, string? id)
    {
        var directory = new AcademicDirectory(Api, Audit);
        var service = new AssistantService(
            new IdentityVerifier(directory),
            _guards,
            Llm,
            new AssistantToolbox(directory, new StudentAuthorizer(directory), Knowledge),
            new CitationValidator(Knowledge),
            Audit,
            Microsoft.Extensions.Options.Options.Create(Options),
            NullLogger<AssistantService>.Instance);
        _answer = await service.AskAsync(new AskQuestionInput(question, new DeclaredUser(role, id)), CancellationToken.None);
        return _answer;
    }

    public Task<AssistantAnswer> AskStudentAsync(string question, string id = "EST-001") =>
        AskAsync(question, UserRoles.Student, id);

    public Task<AssistantAnswer> AskCoordinatorAsync(string question, string id = "COORD-01") =>
        AskAsync(question, UserRoles.Coordinator, id);

    /// <summary>The JSON result of the n-th tool call answered to the model (0-based, across all turns).</summary>
    public JsonElement ToolResult(int index = 0) =>
        Llm.Requests
            .SelectMany(r => r.Messages)
            .Where(m => m.Role == LlmRole.Tool)
            .Distinct()
            .SelectMany(m => m.ToolResults)
            .ElementAt(index)
            .Response;

    /// <summary>Calls to the simulated API that read a student's own resources.</summary>
    public IReadOnlyList<string> StudentDataCalls =>
        Api.Calls.Where(c => c.StartsWith("/estudiantes/", StringComparison.Ordinal)).ToList();

    public static string Final(string answer, params (string Document, string Section)[] citations) =>
        JsonSerializer.Serialize(new
        {
            respuesta = answer,
            citas = citations.Select(c => new { documento = c.Document, seccion = c.Section }),
        });

    private static int StatusOf(AssistantAnswer? answer) => answer?.Outcome switch
    {
        AssistantOutcome.InvalidRequest => 400,
        AssistantOutcome.Forbidden => 403,
        AssistantOutcome.Unavailable => 503,
        _ => 200,
    };
}
