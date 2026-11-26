using TutorFlow.Application.Assistant;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Tests.Support;
using Xunit;

namespace TutorFlow.Application.Tests.Assistant;

/// <summary>
/// Identity existence (R-SEG-04, CL-12, ESC-ENT-13), failures of the API during the check (503, API-02) and the
/// deterministic guard extension point that runs before the LLM (R-SEG-14).
/// </summary>
public class IdentityAndGuardTests
{
    private static void AssertForbiddenBeforeLlm(AssistantHarness h, AssistantAnswer answer, string reason)
    {
        Assert.Equal(AssistantOutcome.Forbidden, answer.Outcome);
        Assert.Equal(AssistantAnswer.ForbiddenText, answer.Text);
        Assert.Equal([AssistantAnswer.ForbiddenCitation], answer.Citations);
        Assert.False(answer.UsedStudentData);
        Assert.Equal(0, answer.Tokens);
        Assert.Equal(0m, answer.CostUsd);
        Assert.Equal(reason, answer.Reason);
        Assert.Equal(0, h.Llm.CallCount);
        Assert.Empty(h.Audit.StudentsConsulted);
        Assert.Same(answer, Assert.Single(h.Audit.Completed));
    }

    // ESC-ENT-13: a well-formed but nonexistent student id is a 403, checked with the program rosters only.
    [Fact]
    public async Task Nonexistent_student_is_403_checked_without_reading_the_student()
    {
        var h = new AssistantHarness();
        var answer = await h.AskStudentAsync("¿Cuál es mi promedio?", "EST-999");

        AssertForbiddenBeforeLlm(h, answer, AuditReasons.InvalidIdentity);
        Assert.Empty(h.StudentDataCalls);
        Assert.Equal(["/programas", "/programas/ING-SIS/estudiantes", "/programas/ADM/estudiantes"], h.Api.Calls);
        Assert.Equal(h.Api.Calls, h.Audit.Resources);
    }

    [Fact]
    public async Task Nonexistent_coordinator_is_403_checked_with_its_own_route()
    {
        var h = new AssistantHarness();
        var answer = await h.AskCoordinatorAsync("¿Qué estudiantes hay en mi programa?", "COORD-09");

        AssertForbiddenBeforeLlm(h, answer, AuditReasons.InvalidIdentity);
        Assert.Equal(["/coordinadores/COORD-09"], h.Api.Calls);
    }

    // ESC-ENT-14, CL-12: role and id mismatch never reaches the API.
    [Theory]
    [InlineData(UserRoles.Student, "COORD-01")]
    [InlineData(UserRoles.Coordinator, "EST-001")]
    public async Task Role_and_id_mismatch_is_403_without_calling_the_api(string role, string id)
    {
        var h = new AssistantHarness();
        var answer = await h.AskAsync("¿Cuál es mi promedio?", role, id);

        AssertForbiddenBeforeLlm(h, answer, AuditReasons.InvalidIdentity);
        Assert.Empty(h.Api.Calls);
    }

    // CL-13, API-02: the API down during the identity check is a 503 and nothing is invented.
    [Theory]
    [InlineData(UserRoles.Student, "EST-001")]
    [InlineData(UserRoles.Coordinator, "COORD-01")]
    public async Task Api_down_during_identity_check_is_503(string role, string id)
    {
        var h = new AssistantHarness();
        h.Api.Stop();
        var answer = await h.AskAsync("¿Cuál es mi promedio?", role, id);

        Assert.Equal(AssistantOutcome.Unavailable, answer.Outcome);
        Assert.Equal(AssistantAnswer.UnavailableText, answer.Text);
        Assert.Empty(answer.Citations);
        Assert.False(answer.UsedStudentData);
        Assert.Equal(0, answer.Tokens);
        Assert.Equal(AuditReasons.DependencyUnavailable, answer.Reason);
        Assert.Equal(0, h.Llm.CallCount);
        Assert.NotEmpty(h.Audit.Resources);
    }

    // ESC-ENT-01: a 400 is also completed in the audit context, with its reason.
    [Fact]
    public async Task Invalid_request_is_completed_with_its_reason()
    {
        var h = new AssistantHarness();
        var answer = await h.AskStudentAsync("   ");

        Assert.Equal(AssistantOutcome.InvalidRequest, answer.Outcome);
        Assert.Equal(AuditReasons.InvalidRequest, Assert.Single(h.Audit.Completed).Reason);
        Assert.Empty(h.Api.Calls);
    }

    // R-SEG-14: a guard runs after the identity check and before the LLM; its block is a 403 with 0 tokens.
    [Fact]
    public async Task Blocking_guard_returns_403_before_the_llm()
    {
        var first = new ScriptedGuard(GuardVerdict.Allowed);
        var second = new ScriptedGuard(GuardVerdict.Blocked(AuditReasons.DeterministicBarrier));
        var third = new ScriptedGuard(GuardVerdict.Allowed);
        var h = new AssistantHarness(first, second, third);

        var answer = await h.AskStudentAsync("¿Cuánto sacó Andrés en Cálculo Integral?");

        AssertForbiddenBeforeLlm(h, answer, AuditReasons.DeterministicBarrier);
        Assert.Equal("EST-001", first.Seen!.User.Id);
        Assert.Equal("¿Cuánto sacó Andrés en Cálculo Integral?", first.Seen.Question);
        Assert.NotNull(second.Seen);
        Assert.Null(third.Seen);
    }

    [Fact]
    public async Task Guard_failing_on_a_dependency_is_503()
    {
        var h = new AssistantHarness(new ScriptedGuard(null));
        var answer = await h.AskStudentAsync("¿Cuál es mi promedio?");

        Assert.Equal(AssistantOutcome.Unavailable, answer.Outcome);
        Assert.Equal(0, h.Llm.CallCount);
    }

    [Fact]
    public async Task Guard_does_not_run_for_an_invalid_identity()
    {
        var guard = new ScriptedGuard(GuardVerdict.Allowed);
        var h = new AssistantHarness(guard);
        await h.AskStudentAsync("¿Cuál es mi promedio?", "EST-999");

        Assert.Null(guard.Seen);
    }

    [Fact]
    public void Guard_verdict_only_accepts_403_reasons_of_the_catalog()
    {
        Assert.Throws<ArgumentException>(() => GuardVerdict.Blocked("otro motivo"));
        Assert.Throws<ArgumentException>(() => GuardVerdict.Blocked(AuditReasons.DependencyUnavailable));
        Assert.True(GuardVerdict.Blocked(AuditReasons.InjectionAttempt).IsBlocked);
        Assert.False(GuardVerdict.Allowed.IsBlocked);
    }

    private sealed class ScriptedGuard(GuardVerdict? verdict) : IQuestionGuard
    {
        public GuardContext? Seen { get; private set; }

        public Task<GuardVerdict> EvaluateAsync(GuardContext context, CancellationToken cancellationToken)
        {
            Seen = context;
            return verdict is null
                ? throw new DependencyUnavailableException(ExternalDependency.AcademicApi, "down")
                : Task.FromResult(verdict);
        }
    }
}
