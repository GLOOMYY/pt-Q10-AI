using System.Text.Json;
using TutorFlow.Integration.Tests.Scripts;
using TutorFlow.Integration.Tests.Support;
using TutorFlow.Scenarios;
using Xunit;

namespace TutorFlow.Integration.Tests.Scenarios;

/// <summary>
/// The runner reads <c>escenarios/</c> following README section 6 and resolves README section 3.3: these tests prove the
/// reading is complete (nothing is silently left out) and that every <c>api</c> scenario has a script.
/// </summary>
public sealed class ScenarioReaderTests
{
    [Fact]
    public void Every_scenario_follows_the_grammar()
    {
        Assert.True(
            ScenarioData.Load.Errors.Count == 0,
            "Format errors: " + string.Join("; ", ScenarioData.Load.Errors.Select(e => $"{e.Id} ({e.File}): {e.Message}")));
    }

    [Fact]
    public void Each_file_has_the_number_of_scenarios_of_the_index()
    {
        var index = ScenarioData.Load.Index;
        Assert.Equal(9, index.Count);
        foreach (var (file, (prefix, count)) in index)
        {
            Assert.Equal(count, ScenarioData.Load.Scenarios.Count(s => s.File == file && s.Id.StartsWith(prefix + "-", StringComparison.Ordinal)));
        }

        Assert.Equal(169, ScenarioData.Load.Scenarios.Count);
        Assert.DoesNotContain(ScenarioData.Load.Scenarios, s => s.Id == "ESC-EJ-01");
        Assert.Equal(ScenarioData.Load.Scenarios.Count, ScenarioData.Load.Scenarios.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void Unit_scenarios_are_not_run_here_and_api_ones_are_all_listed()
    {
        // The 24 unit scenarios belong to Domain.Tests; the other 145 are the api level of this project.
        Assert.Equal(24, ScenarioData.Load.Scenarios.Count(s => s.Level == ScenarioLevel.Unit));
        Assert.Equal(145, ScenarioData.ApiScenarios.Count);
    }

    [Fact]
    public void Every_api_scenario_has_a_script_and_every_script_has_a_scenario()
    {
        var apiIds = ScenarioData.ApiScenarios.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        Assert.All(apiIds, id => Assert.NotNull(ScenarioScripts.For(id)));
        Assert.All(ScenarioScripts.Ids, id => Assert.Contains(id, apiIds));
    }

    [Fact]
    public void The_ten_example_questions_are_the_ESC_BAN_scenarios()
    {
        var root = RepositoryLocator.FindRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "material", "preguntas_ejemplo.json")));
        var questions = document.RootElement.EnumerateArray().ToList();
        Assert.Equal(10, questions.Count);
        foreach (var question in questions)
        {
            var id = $"ESC-BAN-{question.GetProperty("id").GetInt32():00}";
            var body = ScenarioData.Get(id).Input.GetProperty("cuerpo");
            Assert.Equal(question.GetProperty("pregunta").GetString(), body.GetProperty("pregunta").GetString());
            Assert.Equal(question.GetProperty("usuario").GetProperty("rol").GetString(), body.GetProperty("usuario").GetProperty("rol").GetString());
            Assert.Equal(question.GetProperty("usuario").GetProperty("id").GetString(), body.GetProperty("usuario").GetProperty("id").GetString());
        }
    }

    [Fact]
    public void Generated_values_have_the_exact_lengths_of_the_table()
    {
        string Question(string id) => JsonDocument.Parse(ScenarioRequest.From(ScenarioData.Get(id)).Body!).RootElement.GetProperty("pregunta").GetString()!;

        var t1000 = Question("ESC-ENT-03");
        Assert.Equal(1000, t1000.Length);
        Assert.StartsWith("¿Cuál es la nota mínima para aprobar una asignatura?", t1000, StringComparison.Ordinal);
        Assert.EndsWith(" Gracias. Gr", t1000, StringComparison.Ordinal);
        Assert.DoesNotContain(" Gracias. Gracias. Gr Gr", t1000, StringComparison.Ordinal);
        Assert.Equal(52 + (105 * " Gracias.".Length) + " Gr".Length, t1000.Length);

        Assert.Equal(t1000 + "X", Question("ESC-ENT-04"));
        Assert.Equal("   " + t1000 + "   ", Question("ESC-ENT-05"));
        Assert.Equal(1006, Question("ESC-ENT-05").Length);

        var accents = Question("ESC-ENT-06");
        Assert.Equal(1000, accents.Length);
        Assert.Equal(string.Concat(Enumerable.Repeat("ñáéíóú", 166)) + "ñáéí", accents);
        Assert.True(accents.IsNormalized(System.Text.NormalizationForm.FormC));
    }

    [Fact]
    public void An_unknown_generation_marker_is_an_error()
    {
        Assert.Throws<InvalidOperationException>(() => GeneratedValues.Resolve("ESC-ENT-03", "<generar: otra cosa>"));
        Assert.Throws<InvalidOperationException>(() => GeneratedValues.Resolve("ESC-XXX-01", "<generar: 'ñáéíóú' repetido hasta 1000 caracteres>"));
    }
}
