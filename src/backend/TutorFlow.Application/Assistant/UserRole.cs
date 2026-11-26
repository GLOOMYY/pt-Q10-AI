using System.Diagnostics.CodeAnalysis;

namespace TutorFlow.Application.Assistant;

/// <summary>Role declared in <c>usuario.rol</c> (SPEC §4, §5).</summary>
public enum UserRole
{
    /// <summary><c>"estudiante"</c>.</summary>
    Student = 0,

    /// <summary><c>"coordinador"</c>.</summary>
    Coordinator = 1,
}

/// <summary>Contract values of <c>usuario.rol</c> (SPEC §5).</summary>
public static class UserRoles
{
    /// <summary>Wire value of <see cref="UserRole.Student"/>.</summary>
    public const string Student = "estudiante";

    /// <summary>Wire value of <see cref="UserRole.Coordinator"/>.</summary>
    public const string Coordinator = "coordinador";

    /// <summary>
    /// Parses the wire value. Only the exact texts <c>"estudiante"</c> and <c>"coordinador"</c> are valid; anything
    /// else makes the body malformed (400, SPEC §5.0).
    /// </summary>
    /// <param name="value">Raw <c>usuario.rol</c>.</param>
    /// <param name="role">The parsed role when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the value is one of the two contract values.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out UserRole? role)
    {
        role = value switch
        {
            Student => UserRole.Student,
            Coordinator => UserRole.Coordinator,
            _ => null,
        };
        return role is not null;
    }
}
