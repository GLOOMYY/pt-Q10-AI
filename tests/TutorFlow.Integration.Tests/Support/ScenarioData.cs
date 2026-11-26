using TutorFlow.Scenarios;

namespace TutorFlow.Integration.Tests.Support;

/// <summary>
/// Source of the xUnit cases: every <c>api</c> scenario of <c>escenarios/</c>, read with the grammar of README section 6
/// (never copied), as one individual test case per scenario id.
/// </summary>
public static class ScenarioData
{
    private static readonly Lazy<ScenarioLoadResult> Loaded = new(() => ScenarioCatalog.Load());

    /// <summary>Everything read from <c>escenarios/</c>.</summary>
    public static ScenarioLoadResult Load => Loaded.Value;

    /// <summary>The <c>api</c> scenarios, in file order.</summary>
    public static IReadOnlyList<Scenario> ApiScenarios =>
        [.. Load.Scenarios.Where(s => s.Level == ScenarioLevel.Api)];

    /// <summary>Finds a scenario by id.</summary>
    /// <param name="id">Scenario id.</param>
    /// <returns>The scenario.</returns>
    public static Scenario Get(string id) =>
        Load.Scenarios.Single(s => s.Id == id);

    /// <summary>Theory data: one id per <c>api</c> scenario that is not a known defect.</summary>
    /// <returns>The ids.</returns>
    public static IEnumerable<object[]> Runnable() =>
        ApiScenarios.Where(s => !KnownDefects.Contains(s.Id)).Select(s => new object[] { s.Id });
}
