using System.Diagnostics.CodeAnalysis;
using System.Text;
using TutorFlow.Application.Assistant;
using Xunit;

namespace TutorFlow.Application.Tests.Assistant;

/// <summary>Contract validation of the request: SPEC §5.0, D-12, CL-22 and ESC-ENT-01..12.</summary>
public class AskQuestionValidatorTests
{
    private static readonly DeclaredUser Student = new("estudiante", "EST-001");

    // escenarios/README.md §3.3: BASE + " Gracias." repeated and truncated to 1000 characters.
    internal static string T1000()
    {
        const string Base = "¿Cuál es la nota mínima para aprobar una asignatura?";
        var text = new StringBuilder(Base);
        while (text.Length < 1000)
        {
            text.Append(" Gracias.");
        }

        return text.ToString(0, 1000);
    }

    private static bool Validate(string? question, DeclaredUser? user, [NotNullWhen(true)] out AskQuestionCommand? command) =>
        AskQuestionValidator.TryValidate(new AskQuestionInput(question, user), out command);

    [Fact]
    public void T1000_matches_the_scenario_definition()
    {
        var text = T1000();
        Assert.Equal(1000, text.Length);
        Assert.EndsWith("Gr", text, StringComparison.Ordinal);
    }

    // ESC-ENT-01, ESC-ENT-02
    [Theory]
    [InlineData("")]
    [InlineData("     ")]
    public void Empty_or_white_space_question_is_invalid(string question)
    {
        Assert.False(Validate(question, Student, out _));
    }

    // ESC-ENT-03
    [Fact]
    public void Exactly_1000_characters_is_valid()
    {
        Assert.True(Validate(T1000(), Student, out var command));
        Assert.Equal(1000, command.Question.Length);
    }

    // ESC-ENT-04
    [Fact]
    public void Characters_1001_is_invalid()
    {
        Assert.False(Validate(T1000() + "X", Student, out _));
    }

    // ESC-ENT-05: the limit is measured after trimming.
    [Fact]
    public void Leading_and_trailing_spaces_do_not_count()
    {
        Assert.True(Validate("   " + T1000() + "   ", Student, out var command));
        Assert.Equal(T1000(), command.Question);
    }

    // ESC-ENT-06: 1000 characters with accents and ñ (2000 bytes in UTF-8) are valid.
    [Fact]
    public void Length_is_counted_in_characters_not_bytes()
    {
        var text = string.Concat(Enumerable.Repeat("ñáéíóú", 167))[..1000];
        Assert.True(Validate(text, Student, out _));
    }

    // escenarios/README.md §3.3: code points, so a character outside the BMP (2 UTF-16 units) counts once.
    [Fact]
    public void Length_is_counted_in_code_points()
    {
        var astral = char.ConvertFromUtf32(0x1F4DA);
        Assert.True(Validate(string.Concat(Enumerable.Repeat(astral, 1000)), Student, out _));
        Assert.False(Validate(string.Concat(Enumerable.Repeat(astral, 1001)), Student, out _));
    }

    // ESC-ENT-08, ESC-ENT-09, ESC-ENT-12
    [Fact]
    public void Missing_question_or_user_is_invalid()
    {
        Assert.False(Validate(null, Student, out _));
        Assert.False(Validate("¿Cuál es mi promedio?", null, out _));
        Assert.False(Validate(null, null, out _));
    }

    // ESC-ENT-10: only the exact contract values are roles.
    [Theory]
    [InlineData("profesor")]
    [InlineData("Estudiante")]
    [InlineData("")]
    [InlineData(null)]
    public void Role_outside_the_contract_is_invalid(string? role)
    {
        Assert.False(Validate("¿Cuál es mi promedio?", new DeclaredUser(role, "EST-001"), out _));
    }

    // ESC-ENT-07, ESC-ENT-11: invalid JSON or wrong JSON types.
    [Fact]
    public void Malformed_body_is_invalid()
    {
        Assert.False(AskQuestionValidator.TryValidate(AskQuestionInput.Malformed, out _));
    }

    // ESC-ENT-17, D-26: an empty or missing id is not a contract error (it is a 403 later).
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Empty_id_passes_contract_validation(string? id)
    {
        Assert.True(Validate("¿Cuál es mi promedio?", new DeclaredUser("estudiante", id), out var command));
        Assert.Equal(string.Empty, command.DeclaredUserId);
    }

    [Theory]
    [InlineData("estudiante", UserRole.Student)]
    [InlineData("coordinador", UserRole.Coordinator)]
    public void Valid_request_produces_command(string role, UserRole expected)
    {
        Assert.True(Validate("  ¿Cuál es mi promedio?  ", new DeclaredUser(role, " est-001 "), out var command));
        Assert.Equal("¿Cuál es mi promedio?", command.Question);
        Assert.Equal(expected, command.Role);
        Assert.Equal(" est-001 ", command.DeclaredUserId);
    }
}
