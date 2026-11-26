using System.Text.Json.Serialization;
using TutorFlow.Application.Assistant;

namespace TutorFlow.Api.Contracts;

/// <summary><c>usuario</c> of the request body (SPEC §5).</summary>
public sealed class AskQuestionUserDto
{
    /// <summary><c>rol</c>: <c>"estudiante"</c> or <c>"coordinador"</c>.</summary>
    [JsonPropertyName("rol")]
    public string? Role { get; init; }

    /// <summary><c>id</c>: e.g. <c>"EST-001"</c>.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}

/// <summary>Body of <c>POST /api/asistente/preguntar</c> (SPEC §5). Property names are case-sensitive.</summary>
public sealed class AskQuestionRequest
{
    /// <summary><c>pregunta</c>.</summary>
    [JsonPropertyName("pregunta")]
    public string? Question { get; init; }

    /// <summary><c>usuario</c>.</summary>
    [JsonPropertyName("usuario")]
    public AskQuestionUserDto? User { get; init; }

    /// <summary>Maps the body to the raw input of the use case, without validating it.</summary>
    /// <returns>The raw input.</returns>
    public AskQuestionInput ToInput() =>
        new(Question, User is null ? null : new DeclaredUser(User.Role, User.Id));
}
