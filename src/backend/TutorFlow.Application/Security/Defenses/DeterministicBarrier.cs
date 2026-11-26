using TutorFlow.Application.Assistant;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Security.Defenses;

/// <summary>
/// Deterministic barrier of names and ids before the LLM (R-SEG-14, D-28). It builds a block list with the students the
/// user cannot see and blocks the question, with a 403 and no LLM call, if it mentions any of them.
/// </summary>
/// <remarks>
/// <para><b>Block list:</b> built per request from <c>GET /programas</c> and <c>GET /programas/{id}/estudiantes</c>. A
/// student user: every other student. A coordinator: the students of programs not in their charge (taken from
/// <c>GET /coordinadores/{id}</c>, API-08). It never contains the user nor, for a coordinator, students of their programs.</para>
/// <para><b>What is searched:</b> the id, the full name and each word of the name, compared without case, accents or
/// repeated spaces and as whole words (<c>EST-002</c> is not inside <c>EST-0021</c>). Words that are also part of a name
/// the user can see are not searched, so legitimate questions are not rejected. Course codes that look like ids
/// (<c>EST-103</c>, CL-08) are not students and are never in the list.</para>
/// <para><b>Audit:</b> every route requested is recorded with <see cref="IRequestAuditContext.RecordResource"/>, but no
/// student is recorded as consulted: building the list is an authorization step (R-SEG-03, R-LOG-06, D-24).</para>
/// <para>If the API is down, <see cref="Ports.DependencyUnavailableException"/> propagates and becomes a 503: the barrier
/// never lets a question through just because it could not be checked.</para>
/// </remarks>
public sealed class DeterministicBarrier : IQuestionDefense
{
    private readonly IAcademicApi _api;
    private readonly IRequestAuditContext _audit;

    /// <summary>Creates the barrier.</summary>
    /// <param name="api">Port to the simulated academic API.</param>
    /// <param name="audit">Per-request audit context, to record the routes requested.</param>
    public DeterministicBarrier(IAcademicApi api, IRequestAuditContext audit)
    {
        _api = api;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<DefenseVerdict> EvaluateAsync(UserIdentity user, string question, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(question);

        var text = TextNormalizer.Normalize(question);
        if (text.Length == 0)
        {
            return DefenseVerdict.Allow;
        }

        var terms = await BuildBlockedTermsAsync(user, cancellationToken).ConfigureAwait(false);
        foreach (var term in terms)
        {
            if (TextNormalizer.ContainsWholeTerm(text, term))
            {
                return DefenseVerdict.Block(AuditReasons.DeterministicBarrier);
            }
        }

        return DefenseVerdict.Allow;
    }

    private async Task<HashSet<string>> BuildBlockedTermsAsync(UserIdentity user, CancellationToken cancellationToken)
    {
        var programsInCharge = await GetProgramsInChargeAsync(user, cancellationToken).ConfigureAwait(false);

        _audit.RecordResource(AcademicApiRoutes.Programs);
        var programs = await _api.GetProgramsAsync(cancellationToken).ConfigureAwait(false);

        var blocked = new List<StudentRecord>();
        var visible = new List<StudentRecord>();
        foreach (var program in programs)
        {
            // API-01: ids coming from the API are validated before they are put in a route.
            if (!ProgramId.TryParse(program.Id, out var programId))
            {
                continue;
            }

            _audit.RecordResource(AcademicApiRoutes.ProgramStudents(programId));
            var students = await _api.GetProgramStudentsAsync(programId, cancellationToken).ConfigureAwait(false);
            foreach (var student in students ?? [])
            {
                if (IsVisible(user, student, programId, programsInCharge))
                {
                    visible.Add(student);
                }
                else
                {
                    blocked.Add(student);
                }
            }
        }

        // The exception of R-SEG-14: words of names the user can see are not searched.
        var visibleWords = new HashSet<string>(StringComparer.Ordinal);
        foreach (var student in visible)
        {
            visibleWords.UnionWith(Words(student.Name));
        }

        var terms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var student in blocked)
        {
            AddIfNotEmpty(terms, TextNormalizer.Normalize(student.Id));
            AddIfNotEmpty(terms, TextNormalizer.Normalize(student.Name));
            foreach (var word in Words(student.Name))
            {
                if (!visibleWords.Contains(word))
                {
                    terms.Add(word);
                }
            }
        }

        return terms;
    }

    private async Task<HashSet<string>> GetProgramsInChargeAsync(UserIdentity user, CancellationToken cancellationToken)
    {
        var inCharge = new HashSet<string>(StringComparer.Ordinal);
        if (user.Role != UserRole.Coordinator)
        {
            return inCharge;
        }

        // API-08: the coordinator-program relation comes only from the coordinator record. If it cannot be found the set
        // stays empty and every student is blocked (the safe side).
        _audit.RecordResource(AcademicApiRoutes.Coordinator(user.CoordinatorId!));
        var coordinator = await _api.GetCoordinatorAsync(user.CoordinatorId!, cancellationToken).ConfigureAwait(false);
        foreach (var id in coordinator?.ProgramIds ?? [])
        {
            if (ProgramId.TryParse(id, out var programId))
            {
                inCharge.Add(programId.Value);
            }
        }

        return inCharge;
    }

    private static bool IsVisible(
        UserIdentity user,
        StudentRecord student,
        ProgramId studentProgram,
        HashSet<string> programsInCharge) =>
        user.Role == UserRole.Student
            ? string.Equals(student.Id, user.Id, StringComparison.OrdinalIgnoreCase)
            : programsInCharge.Contains(studentProgram.Value);

    private static string[] Words(string name) =>
        TextNormalizer.Normalize(name).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static void AddIfNotEmpty(HashSet<string> set, string value)
    {
        if (value.Length > 0)
        {
            set.Add(value);
        }
    }
}
