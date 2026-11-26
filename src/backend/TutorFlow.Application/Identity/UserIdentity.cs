using System.Diagnostics.CodeAnalysis;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Ports.AcademicApi;

namespace TutorFlow.Application.Identity;

/// <summary>
/// The identity of the session: role and normalized id taken only from <c>usuario</c> in the body (R-SEG-09, D-10).
/// Tools receive it from here, never as a parameter chosen by the model (R-SEG-06).
/// </summary>
/// <remarks>
/// <see cref="TryCreate"/> only checks the format and the coherence between role and id, without calling the API
/// (R-SEG-04, API-01). Existence is checked afterwards by the use case.
/// </remarks>
public sealed record UserIdentity
{
    private UserIdentity(UserRole role, StudentId? studentId, CoordinatorId? coordinatorId)
    {
        Role = role;
        StudentId = studentId;
        CoordinatorId = coordinatorId;
    }

    /// <summary>The declared role.</summary>
    public UserRole Role { get; }

    /// <summary>The student id when <see cref="Role"/> is <see cref="UserRole.Student"/>; otherwise <see langword="null"/>.</summary>
    public StudentId? StudentId { get; }

    /// <summary>The coordinator id when <see cref="Role"/> is <see cref="UserRole.Coordinator"/>; otherwise <see langword="null"/>.</summary>
    public CoordinatorId? CoordinatorId { get; }

    /// <summary>The normalized id (e.g. <c>EST-001</c>, <c>COORD-01</c>).</summary>
    public string Id => StudentId?.Value ?? CoordinatorId!.Value;

    /// <summary>
    /// Normalizes the id (trim + upper case, API-05) and checks that its format matches the role (R-SEG-04): a student
    /// needs <c>EST-ddd</c> and a coordinator <c>COORD-dd</c>. An empty id, invalid characters (<c>%</c>, <c>/</c>,
    /// <c>..</c>) or a role/id mismatch fail (403, CL-12, D-26).
    /// </summary>
    /// <param name="role">The declared role.</param>
    /// <param name="declaredId">The raw declared id.</param>
    /// <param name="identity">The identity when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the format is valid for the role.</returns>
    public static bool TryCreate(UserRole role, string? declaredId, [NotNullWhen(true)] out UserIdentity? identity)
    {
        identity = role switch
        {
            UserRole.Student when Ports.AcademicApi.StudentId.TryParse(declaredId, out var student) =>
                new UserIdentity(role, student, null),
            UserRole.Coordinator when Ports.AcademicApi.CoordinatorId.TryParse(declaredId, out var coordinator) =>
                new UserIdentity(role, null, coordinator),
            _ => null,
        };
        return identity is not null;
    }
}
