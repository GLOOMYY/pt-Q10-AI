using TutorFlow.Application.Academic;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports.AcademicApi;

namespace TutorFlow.Application.Authorization;

/// <summary>
/// A student whose academic data the session user may see, decided by <see cref="StudentAuthorizer"/>. Its constructor
/// is internal, so student data can only be read after the deterministic authorization (R-SEG-01).
/// </summary>
public sealed record AuthorizedStudent
{
    internal AuthorizedStudent(StudentId id, StudentRecord record, ProgramId programId)
    {
        Id = id;
        Record = record;
        ProgramId = programId;
    }

    /// <summary>The student id.</summary>
    public StudentId Id { get; }

    /// <summary>The roster entry (id, name, program, semester).</summary>
    public StudentRecord Record { get; }

    /// <summary>The program through which access was granted (the student's own program, or one in the
    /// coordinator's charge). Used for the data citation of a coordinator (R-RES-02).</summary>
    public ProgramId ProgramId { get; }
}

/// <summary>
/// Deterministic authorization with the matrix of SPEC §4, before any call for student data (R-SEG-01..03):
/// <list type="bullet">
/// <item><description>Student: only their own id (R-SEG-02).</description></item>
/// <item><description>Coordinator: only students listed in the rosters of the programs in charge; the requested
/// student is never consulted to decide (R-SEG-03).</description></item>
/// </list>
/// </summary>
public sealed class StudentAuthorizer
{
    private readonly IAcademicDirectory _directory;

    /// <summary>Creates the authorizer.</summary>
    /// <param name="directory">The per-request academic directory.</param>
    public StudentAuthorizer(IAcademicDirectory directory) => _directory = directory;

    /// <summary>Authorizes access to a student's academic data.</summary>
    /// <param name="user">The session user (R-SEG-06).</param>
    /// <param name="requestedId">For a coordinator, the requested student (required). For a student,
    /// <see langword="null"/> or their own id; anything else is denied.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The authorized student, or <see langword="null"/> if denied (403).</returns>
    public async Task<AuthorizedStudent?> AuthorizeAsync(
        SessionUser user,
        StudentId? requestedId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsStudent)
        {
            // R-SEG-02, R-SEG-06: a student only reaches their own id, taken from the session.
            var own = user.Identity.StudentId!;
            if ((requestedId is not null && requestedId != own) || user.StudentRecord is null || user.ProgramIds.Count == 0)
            {
                return null;
            }

            return new AuthorizedStudent(own, user.StudentRecord, user.ProgramIds[0]);
        }

        if (requestedId is null)
        {
            return null;
        }

        // R-SEG-03: (1) programs in charge from GET /coordinadores/{id} (already in the session); (2) their rosters;
        // (3) allowed only if the requested student is listed. Never GET /estudiantes/{otherId} to decide.
        foreach (var programId in user.ProgramIds)
        {
            var roster = await _directory.GetProgramStudentsAsync(programId, cancellationToken).ConfigureAwait(false);
            var record = roster?.FirstOrDefault(s => string.Equals(s.Id, requestedId.Value, StringComparison.Ordinal));
            if (record is not null)
            {
                return new AuthorizedStudent(requestedId, record, programId);
            }
        }

        return null;
    }

    /// <summary>Whether a coordinator is in charge of a program (§4: program lists only of own programs).</summary>
    /// <param name="user">The session user.</param>
    /// <param name="programId">The program.</param>
    /// <returns><see langword="true"/> only for a coordinator in charge of the program.</returns>
    public static bool CanSeeProgram(SessionUser user, ProgramId programId)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.IsCoordinator && user.ProgramIds.Contains(programId);
    }
}
