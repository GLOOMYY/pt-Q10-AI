using System.Text.RegularExpressions;
using TutorFlow.Domain.Tests.Scenarios;

namespace TutorFlow.Domain.Tests;

/// <summary>Source guards for D-06, R-CAL-02 and R-CAL-03: the domain never uses binary floating point or default rounding.</summary>
public partial class RoundingGuardTests
{
    [GeneratedRegex(@"Math\.Round\(([^;]*)\)")]
    private static partial Regex RoundCall();

    [GeneratedRegex(@"\b(double|float)\b")]
    private static partial Regex FloatingPoint();

    private static IEnumerable<(string File, string Code)> DomainSources() =>
        Directory.EnumerateFiles(Path.Combine(ScenarioCatalog.RepositoryRoot, "src", "backend", "TutorFlow.Domain"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => (f, File.ReadAllText(f)));

    [Fact]
    public void Every_Math_Round_in_the_domain_uses_AwayFromZero_D_06()
    {
        var calls = DomainSources().SelectMany(s => RoundCall().Matches(s.Code).Select(m => (s.File, Call: m.Value))).ToList();
        Assert.NotEmpty(calls);
        Assert.All(calls, c => Assert.Contains("MidpointRounding.AwayFromZero", c.Call, StringComparison.Ordinal));
    }

    [Fact]
    public void The_domain_does_not_use_double_or_float_R_CAL_02()
    {
        // Comments are stripped: the text may mention double to explain why it is forbidden.
        var offenders = DomainSources()
            .Where(s => FloatingPoint().IsMatch(Regex.Replace(s.Code, @"//.*|/\*.*?\*/", string.Empty, RegexOptions.Singleline)))
            .Select(s => s.File);
        Assert.Empty(offenders);
    }
}
