using TutorFlow.Application.Assistant;
using TutorFlow.Application.Ports.AcademicApi;

namespace TutorFlow.Application.Identity;

/// <summary>
/// The verified user of the request (the "session" of R-SEG-06): the identity declared in <c>usuario</c>, checked for
/// format and existence against the simulated API (R-SEG-04). Tools take the user from here, never from the model.
/// </summary>
public sealed record SessionUser
{
    /// <summary>Creates a verified session user. Only <see cref="IdentityVerifier"/> (and tests) build it.</summary>
    /// <param name="identity">The identity, with a valid format.</param>
    /// <param name="programIds">Student: the program(s) whose roster lists the student. Coordinator: the programs in
    /// charge, from <c>GET /coordinadores/{id}</c> (API-08).</param>
    /// <param name="studentRecord">Student: the student's own entry of the roster; <see langword="null"/> for a
    /// coordinator.</param>
    public SessionUser(UserIdentity identity, IReadOnlyList<ProgramId> programIds, StudentRecord? studentRecord)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(programIds);
        Identity = identity;
        ProgramIds = programIds;
        StudentRecord = studentRecord;
    }

    /// <summary>The identity (role and normalized id).</summary>
    public UserIdentity Identity { get; }

    /// <summary>The role.</summary>
    public UserRole Role => Identity.Role;

    /// <summary>The normalized id, e.g. <c>EST-001</c> or <c>COORD-01</c>.</summary>
    public string Id => Identity.Id;

    /// <summary>Student: own program(s). Coordinator: programs in charge.</summary>
    public IReadOnlyList<ProgramId> ProgramIds { get; }

    /// <summary>Student: the own roster entry (id, name, program, semester). <see langword="null"/> for a coordinator.</summary>
    public StudentRecord? StudentRecord { get; }

    /// <summary>Whether the user is a student.</summary>
    public bool IsStudent => Role == UserRole.Student;

    /// <summary>Whether the user is a coordinator.</summary>
    public bool IsCoordinator => Role == UserRole.Coordinator;
}
