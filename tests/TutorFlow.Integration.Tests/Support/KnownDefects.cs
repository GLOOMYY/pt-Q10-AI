namespace TutorFlow.Integration.Tests.Support;

/// <summary>
/// Explicit, visible list of scenarios that currently fail because of a defect of the backend (never of the script, the
/// scenario or the assertion). Each entry has the reason, the probable owner and what was observed. A listed scenario is
/// still executed by <c>ApiScenarioTests.Known_defect_still_fails</c>, which fails as soon as the defect is fixed so the
/// entry gets removed; nothing is skipped silently (AGENTS section 8). The goal is for this list to be empty.
/// </summary>
public static class KnownDefects
{
    private static readonly Dictionary<string, string> Entries = new(StringComparer.Ordinal)
    {
    };

    /// <summary>Ids of the excluded scenarios.</summary>
    public static IReadOnlyCollection<string> Ids => Entries.Keys;

    /// <summary>Whether the scenario is a known defect.</summary>
    /// <param name="scenarioId">Scenario id.</param>
    /// <returns><see langword="true"/> if listed.</returns>
    public static bool Contains(string scenarioId) => Entries.ContainsKey(scenarioId);

    /// <summary>Reason and owner of a known defect.</summary>
    /// <param name="scenarioId">Scenario id.</param>
    /// <returns>The description.</returns>
    public static string Describe(string scenarioId) => Entries[scenarioId];
}
