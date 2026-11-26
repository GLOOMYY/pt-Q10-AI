using TutorFlow.Scenarios;
using Xunit;

namespace TutorFlow.Integration.Tests.Scenarios;

/// <summary>Comparison rules of escenarios/README section 4.1 (used by <c>evals/</c> for contiene, noContiene and log.noContiene).</summary>
public sealed class TextMatchingTests
{
    [Theory]
    [InlineData("Artículo 5", "articulo 5")]
    [InlineData("ARTÍCULO", "articulo")]
    [InlineData("Matemáticas básicas", "matematicas basicas")]
    public void Case_and_accents_are_ignored(string text, string fragment) =>
        Assert.True(TextMatching.Contains(text, fragment));

    [Fact]
    public void The_enie_is_kept_and_differs_from_n()
    {
        Assert.False(TextMatching.Contains("el ano pasado", "año"));
        Assert.False(TextMatching.Contains("el año pasado", "ano"));
        Assert.True(TextMatching.Contains("el AÑO pasado", "año"));
    }

    [Theory]
    [InlineData("la nota es 13.0", "3.0", false)]
    [InlineData("la nota es 3.05", "3.0", false)]
    [InlineData("la nota es 3.0.", "3.0", true)]
    [InlineData("la nota es 3.0", "3.0", true)]
    [InlineData("el corte es 12.40", "2.40", false)]
    [InlineData("acumulada 2.40 puntos", "2.40", true)]
    [InlineData("en la semana 8.", "8", true)]
    [InlineData("en la semana 18", "8", false)]
    [InlineData("en la semana 8.5", "8", false)]
    [InlineData("la nota es 3,0", "3.0", false)]
    public void Numeric_fragments_only_match_on_a_numeric_boundary(string text, string fragment, bool expected) =>
        Assert.Equal(expected, TextMatching.Contains(text, fragment));

    [Fact]
    public void A_fragment_that_does_not_start_or_end_with_a_digit_has_no_boundary()
    {
        Assert.True(TextMatching.Contains("Laura Gómez tiene 4.39", "Laura"));
        Assert.True(TextMatching.Contains("5 días hábiles", "5 días hábiles"));
        Assert.True(TextMatching.Contains("15%", "15"));
    }
}
