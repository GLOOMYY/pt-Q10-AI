using System.Diagnostics.CodeAnalysis;

namespace TutorFlow.Application.Assistant;

/// <summary>
/// Contract validation of the request (SPEC §5.0, D-12, CL-22). A failure is a 400 with the standard body, decided
/// before any call to the simulated API or the LLM.
/// </summary>
public static class AskQuestionValidator
{
    /// <summary>
    /// Maximum length of the question, in Unicode code points, after trimming (SPEC §5.0, D-12). It is part of the API
    /// contract, shared with the frontend (R-FRO-02), not a tunable setting.
    /// </summary>
    public const int MaxQuestionLength = 1000;

    /// <summary>
    /// Validates the raw input. Invalid: malformed body, missing <c>pregunta</c> or <c>usuario</c>, a <c>rol</c> other
    /// than <c>"estudiante"</c> or <c>"coordinador"</c>, or a question that is empty, white space only or longer than
    /// <see cref="MaxQuestionLength"/> code points after trimming.
    /// </summary>
    /// <param name="input">The raw input.</param>
    /// <param name="command">The validated command when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the input follows the contract.</returns>
    /// <remarks>
    /// The id is not validated here: an empty, missing or malformed <c>usuario.id</c> is an invalid identity (403,
    /// R-SEG-04, D-26), not a malformed body.
    /// </remarks>
    public static bool TryValidate(AskQuestionInput input, [NotNullWhen(true)] out AskQuestionCommand? command)
    {
        ArgumentNullException.ThrowIfNull(input);
        command = null;

        if (input.IsMalformed || input.Question is null || input.User is null)
        {
            return false;
        }

        if (!UserRoles.TryParse(input.User.Role, out var role))
        {
            return false;
        }

        var question = input.Question.Trim();
        var length = CountCodePoints(question);
        if (length == 0 || length > MaxQuestionLength)
        {
            return false;
        }

        command = new AskQuestionCommand(question, role.Value, input.User.Id ?? string.Empty);
        return true;
    }

    // escenarios/README.md §3.3: lengths are measured in Unicode code points, not in UTF-16 units nor bytes.
    private static int CountCodePoints(string text)
    {
        var count = 0;
        foreach (var _ in text.EnumerateRunes())
        {
            count++;
        }

        return count;
    }
}
