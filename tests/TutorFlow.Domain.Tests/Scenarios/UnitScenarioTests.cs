using System.Text.Json;
using TutorFlow.Domain.Attendance;
using TutorFlow.Domain.Grading;

namespace TutorFlow.Domain.Tests.Scenarios;

/// <summary>
/// Runs every <c>unitario</c> scenario of ESC-RAR and ESC-SIM straight from <c>escenarios/</c> (R-QA-02). The
/// canonical function names of escenarios/README.md §4.2 are mapped to the domain API here; special results
/// (<c>sin_promedio</c>, <c>no_determinable</c>, ...) are mapped from the domain's typed results.
/// </summary>
public class UnitScenarioTests
{
    private const string RareFile = "casos_raros.md";
    private const string RarePrefix = "ESC-RAR";
    private const string SimulationFile = "simulacion.md";
    private const string SimulationPrefix = "ESC-SIM";

    private static readonly Lazy<IReadOnlyList<UnitScenario>> Rare = new(() => ScenarioCatalog.LoadUnit(RareFile, RarePrefix));
    private static readonly Lazy<IReadOnlyList<UnitScenario>> Simulation = new(() => ScenarioCatalog.LoadUnit(SimulationFile, SimulationPrefix));

    private static readonly string[] KnownFunctions =
        ["CalcularDefinitiva", "CalcularPromedio", "PierdePorInasistencia", "CalcularNotaNecesaria"];

    /// <summary>Scenario ids of one canonical function, across both files.</summary>
    public static TheoryData<string> IdsOf(string function)
    {
        var data = new TheoryData<string>();
        foreach (UnitScenario s in Rare.Value.Concat(Simulation.Value).Where(s => s.Function == function))
        {
            data.Add(s.Id);
        }

        return data;
    }

    public static TheoryData<string> DefinitiveIds => IdsOf("CalcularDefinitiva");

    public static TheoryData<string> AverageIds => IdsOf("CalcularPromedio");

    public static TheoryData<string> AbsenceIds => IdsOf("PierdePorInasistencia");

    public static TheoryData<string> RequiredGradeIds => IdsOf("CalcularNotaNecesaria");

    [Fact]
    public void AllUnitScenariosAreLoadedAndUseKnownFunctions()
    {
        // ESC-RAR-01..17 and ESC-SIM-01..07 are the unit ones; the rest are api level.
        string[] expectedRare = [.. Enumerable.Range(1, 17).Select(n => $"ESC-RAR-{n:00}")];
        string[] expectedSimulation = [.. Enumerable.Range(1, 7).Select(n => $"ESC-SIM-{n:00}")];

        Assert.Equal(expectedRare, Rare.Value.Select(s => s.Id));
        Assert.Equal(expectedSimulation, Simulation.Value.Select(s => s.Id));
        Assert.All(Rare.Value.Concat(Simulation.Value), s => Assert.Contains(s.Function, KnownFunctions));
    }

    [Theory]
    [MemberData(nameof(DefinitiveIds))]
    public void CalcularDefinitiva(string id) =>
        AssertScenario(id, a => GradeCalculator.CalculateFinalGrade(
            a.GetProperty("corte1").GetDecimal(), a.GetProperty("corte2").GetDecimal(), a.GetProperty("corte3").GetDecimal()));

    [Theory]
    [MemberData(nameof(AverageIds))]
    public void CalcularPromedio(string id) =>
        AssertScenario(id, a =>
        {
            IEnumerable<WeightedGrade> grades = a.GetProperty("asignaturas").EnumerateArray()
                .Select(e => new WeightedGrade(e.GetProperty("definitiva").GetDecimal(), e.GetProperty("creditos").GetInt32()));
            decimal? average = GradeCalculator.CalculateAverage(grades);
            return average is null ? "sin_promedio" : average.Value;
        });

    [Theory]
    [MemberData(nameof(AbsenceIds))]
    public void PierdePorInasistencia(string id) =>
        AssertScenario(id, a =>
        {
            JsonElement absences = a.GetProperty("inasistencias");
            int? value = absences.ValueKind == JsonValueKind.Null ? null : absences.GetInt32();
            return AttendancePolicy.Evaluate(value, a.GetProperty("clasesProgramadas").GetInt32()) switch
            {
                AbsenceStatus.Lost => true,
                AbsenceStatus.NotLost => false,
                _ => "no_determinable",
            };
        });

    [Theory]
    [MemberData(nameof(RequiredGradeIds))]
    public void CalcularNotaNecesaria(string id) =>
        AssertScenario(id, a =>
        {
            RequiredGradeResult r = RequiredGradeSimulator.Calculate(
                Cut(a, "corte1"), Cut(a, "corte2"), Cut(a, "corte3"), a.GetProperty("meta").GetDecimal());
            return r.Outcome switch
            {
                SimulationOutcome.Required => r.Grade!.Value,
                SimulationOutcome.Unreachable => "inalcanzable",
                SimulationOutcome.Secured => "asegurada",
                SimulationOutcome.NoPendingCuts => "sin_cortes_pendientes",
                _ => throw new InvalidOperationException($"Unexpected outcome {r.Outcome}."),
            };
        });

    private static decimal? Cut(JsonElement arguments, string name)
    {
        JsonElement value = arguments.GetProperty(name);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetDecimal();
    }

    private static void AssertScenario(string id, Func<JsonElement, object> run)
    {
        UnitScenario scenario = Rare.Value.Concat(Simulation.Value).Single(s => s.Id == id);
        object expected = scenario.Expected.ValueKind switch
        {
            JsonValueKind.Number => scenario.Expected.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => scenario.Expected.GetString()!,
            _ => throw new InvalidDataException($"{id}: unsupported expected result."),
        };

        Assert.Equal(expected, run(scenario.Arguments));
    }
}
