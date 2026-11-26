namespace TutorFlow.Application.Assistant;

/// <summary>The user as declared in the body (<c>usuario</c>), before any validation.</summary>
/// <param name="Role">Raw <c>usuario.rol</c>; <see langword="null"/> if absent.</param>
/// <param name="Id">Raw <c>usuario.id</c>; <see langword="null"/> if absent.</param>
public sealed record DeclaredUser(string? Role, string? Id);

/// <summary>
/// Raw input of <c>POST /api/asistente/preguntar</c> as the inbound adapter read it. Every request goes through the
/// use case, even a malformed one, so a single place decides the status and the audit record (R-LOG-01).
/// </summary>
/// <param name="Question">Raw <c>pregunta</c>; <see langword="null"/> if absent.</param>
/// <param name="User">Raw <c>usuario</c>; <see langword="null"/> if absent.</param>
/// <param name="IsMalformed"><see langword="true"/> when the body is not valid JSON or a field has the wrong JSON type
/// (e.g. <c>"pregunta": 123</c>).</param>
public sealed record AskQuestionInput(string? Question, DeclaredUser? User, bool IsMalformed = false)
{
    /// <summary>Input for a body that could not be read as the contract (invalid JSON or wrong types).</summary>
    public static AskQuestionInput Malformed { get; } = new(null, null, IsMalformed: true);
}

/// <summary>A request that follows the contract (SPEC §5), ready for the identity check.</summary>
/// <param name="Question">The question without leading or trailing white space, 1 to 1 000 characters.</param>
/// <param name="Role">The declared role.</param>
/// <param name="DeclaredUserId">The declared id, still raw (it can be empty or invalid: that is a 403, D-26).</param>
public sealed record AskQuestionCommand(string Question, UserRole Role, string DeclaredUserId);
