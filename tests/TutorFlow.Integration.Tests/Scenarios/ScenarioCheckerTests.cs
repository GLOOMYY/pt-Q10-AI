using System.Text.Json;
using TutorFlow.Scenarios;
using Xunit;

namespace TutorFlow.Integration.Tests.Scenarios;

/// <summary>
/// The checker applies the table of escenarios/README section 4.4 (which fields each site checks) and the comparison
/// rules. These tests use synthetic scenarios to prove a wrong output is detected (the runner cannot pass by accident).
/// </summary>
public sealed class ScenarioCheckerTests
{
    private static readonly ScenarioChecker Checker = new(GlobalCitationRules.Load());

    private static Scenario Make(string expected, params string[] verifies) =>
        new(
            "ESC-TST-01",
            "synthetic",
            "none.md",
            verifies.Length == 0 ? ["R-QA-01"] : verifies,
            ScenarioLevel.Api,
            Precondition.ApiUp,
            JsonDocument.Parse("{}").RootElement.Clone(),
            JsonDocument.Parse(expected).RootElement.Clone());

    private static ObservedResult Observe(int status, string body, string? record = null, params int[] after) =>
        new(status, body, record is null ? null : JsonDocument.Parse(record).RootElement.Clone(), after);

    private const string Ok200 =
        """{"respuesta":"La nota mínima es 3.0","citas":[{"documento":"reglamento_academico_2025.md","seccion":"Artículo 5"}],"usoDatosEstudiante":false,"tokens":120,"costoUsd":0.0001}""";

    [Fact]
    public void Tests_do_not_check_what_depends_on_the_model_text()
    {
        var scenario = Make("""{"status":200,"respuesta":{"contiene":["inexistente"],"noContiene":["3.0"]},"citas":{"exactas":[{"documento":"x.md","seccion":"Y"}]},"tokens":">0","costoUsd":">0"}""");

        var outcome = Checker.Check(scenario, Observe(200, Ok200), CheckSite.Tests);

        Assert.True(outcome.Passed);
        Assert.Equal(1, outcome.ChecksRun); // only the status
    }

    [Fact]
    public void Evals_check_the_text_the_citations_of_a_200_and_the_tokens()
    {
        var scenario = Make("""{"status":200,"respuesta":{"contiene":["inexistente"],"noContiene":["3.0"]},"citas":{"exactas":[{"documento":"x.md","seccion":"Y"}]},"tokens":">0","costoUsd":">0"}""");

        var outcome = Checker.Check(scenario, Observe(200, Ok200), CheckSite.Evals);

        Assert.Contains(outcome.Failures, f => f.StartsWith("respuesta.contiene", StringComparison.Ordinal));
        Assert.Contains(outcome.Failures, f => f.StartsWith("respuesta.noContiene", StringComparison.Ordinal));
        Assert.Contains(outcome.Failures, f => f.StartsWith("citas.exactas", StringComparison.Ordinal));
        Assert.Equal(1, outcome.Failures.Count(f => f.Contains("citas", StringComparison.Ordinal)));
    }

    [Fact]
    public void Status_igual_and_zero_tokens_are_checked_everywhere()
    {
        var scenario = Make("""{"status":403,"respuesta":{"igual":"No estás autorizado a obtener esa información."},"citas":{"exactas":[{"documento":"reglamento_academico_2025.md","seccion":"Artículo 22"}]},"usoDatosEstudiante":false,"tokens":0,"costoUsd":0}""");
        const string forbidden = """{"respuesta":"No estás autorizado a obtener esa información.","citas":[{"documento":"reglamento_academico_2025.md","seccion":"Artículo 22"}],"usoDatosEstudiante":false,"tokens":0,"costoUsd":0}""";

        foreach (var site in new[] { CheckSite.Tests, CheckSite.Evals })
        {
            Assert.True(Checker.Check(scenario, Observe(403, forbidden), site).Passed);
            Assert.False(Checker.Check(scenario, Observe(200, forbidden), site).Passed);
            Assert.False(Checker.Check(scenario, Observe(403, forbidden.Replace("\"tokens\":0", "\"tokens\":5", StringComparison.Ordinal)), site).Passed);
            Assert.False(Checker.Check(scenario, Observe(403, forbidden.Replace("No estás", "No estas", StringComparison.Ordinal)), site).Passed);
        }
    }

    [Fact]
    public void Citations_are_compared_as_a_multiset_and_the_global_list_is_applied_to_errors()
    {
        var scenario = Make("""{"status":403,"citas":{"exactas":[{"documento":"reglamento_academico_2025.md","seccion":"Artículo 22"}]}}""");
        const string duplicated = """{"respuesta":"x","citas":[{"documento":"reglamento_academico_2025.md","seccion":"Artículo 22"},{"documento":"reglamento_academico_2025.md","seccion":"Artículo 22"}]}""";
        const string prohibited = """{"respuesta":"x","citas":[{"documento":"reglamento_academico_2023.md","seccion":"Artículo 22"}]}""";
        const string notCitable = """{"respuesta":"x","citas":[{"documento":"bienestar_y_becas.md","seccion":"Sección 3"},{"documento":"reglamento_academico_2025.md","seccion":"Artículo 22"}]}""";

        Assert.False(Checker.Check(scenario, Observe(403, duplicated), CheckSite.Tests).Passed);
        Assert.Contains(Checker.Check(scenario, Observe(403, prohibited), CheckSite.Tests).Failures, f => f.Contains("global", StringComparison.Ordinal));
        Assert.Contains(Checker.Check(scenario, Observe(403, notCitable), CheckSite.Tests).Failures, f => f.Contains("global", StringComparison.Ordinal));
    }

    [Fact]
    public void The_global_check_of_a_200_runs_only_in_evals()
    {
        var scenario = Make("""{"status":200}""");
        const string bad = """{"respuesta":"x","citas":[{"documento":"reglamento_academico_2023.md","seccion":"Artículo 5"}],"usoDatosEstudiante":false,"tokens":1,"costoUsd":0.1}""";

        Assert.True(Checker.Check(scenario, Observe(200, bad), CheckSite.Tests).Passed);
        Assert.False(Checker.Check(scenario, Observe(200, bad), CheckSite.Evals).Passed);
    }

    [Fact]
    public void Data_citations_use_the_period_and_the_program_ids_of_datos_json()
    {
        var rules = GlobalCitationRules.Load();
        Assert.True(rules.IsAllowed(new ObservedCitation("Datos del estudiante", "Notas · periodo 2026-2")));
        Assert.True(rules.IsAllowed(new ObservedCitation("Datos del estudiante", "Asistencia · periodo 2026-2")));
        Assert.True(rules.IsAllowed(new ObservedCitation("Datos del programa", "ADM · periodo 2026-2")));
        Assert.True(rules.IsAllowed(new ObservedCitation("Datos del programa", "ING-SIS · periodo 2026-2")));
        Assert.False(rules.IsAllowed(new ObservedCitation("Datos del programa", "XYZ · periodo 2026-2")));
        Assert.False(rules.IsAllowed(new ObservedCitation("Datos del estudiante", "Notas · periodo 2026-1")));
        Assert.False(rules.IsAllowed(new ObservedCitation("bienestar_y_becas.md", "Anexo")));
    }

    [Fact]
    public void Log_checks_use_the_audit_record_and_the_rules_of_section_5_3()
    {
        var scenario = Make("""{"log":{"estudiantesConsultados":["EST-001"],"status":200,"motivo":null,"noContiene":["4.39","Laura"]}}""");
        const string good = """{"estudiantesConsultados":["EST-001"],"status":200,"motivo":null,"pregunta":"¿Laura Gómez tiene 4.39?","tokens":4.39,"costoUsd":4.39,"duracionMs":4,"herramientas":["consultar_situacion_academica"]}""";
        const string leak = """{"estudiantesConsultados":["EST-001"],"status":200,"motivo":null,"herramientas":["Laura Gómez 4.39"]}""";

        Assert.True(Checker.Check(scenario, Observe(200, Ok200, good), CheckSite.Tests).Passed);
        var failures = Checker.Check(scenario, Observe(200, Ok200, leak), CheckSite.Tests).Failures;
        Assert.Equal(2, failures.Count(f => f.StartsWith("log.noContiene", StringComparison.Ordinal)));
        Assert.Contains(Checker.Check(scenario, Observe(200, Ok200), CheckSite.Tests).Failures, f => f.Contains("not found", StringComparison.Ordinal));
        Assert.False(Checker.Check(scenario, Observe(200, Ok200, good.Replace("EST-001", "EST-002", StringComparison.Ordinal)), CheckSite.Tests).Passed);
        Assert.False(Checker.Check(scenario, Observe(200, Ok200, good.Replace("\"motivo\":null", "\"motivo\":\"x\"", StringComparison.Ordinal)), CheckSite.Tests).Passed);
    }

    [Fact]
    public void Log_students_of_a_403_by_rechazar_is_checked_only_in_tests()
    {
        // D-32: with the real LLM the model may query data before rejecting.
        var scenario = Make("""{"status":403,"log":{"estudiantesConsultados":[]}}""", "R-ARQ-04");
        const string record = """{"estudiantesConsultados":["EST-001"],"status":403}""";
        const string body = """{"respuesta":"x","citas":[],"usoDatosEstudiante":false,"tokens":0,"costoUsd":0}""";

        Assert.False(Checker.Check(scenario, Observe(403, body, record), CheckSite.Tests).Passed);
        Assert.True(Checker.Check(scenario, Observe(403, body, record), CheckSite.Evals).Passed);

        var other = Make("""{"status":403,"log":{"estudiantesConsultados":[]}}""", "R-SEG-07");
        Assert.False(Checker.Check(other, Observe(403, body, record), CheckSite.Evals).Passed);
    }

    [Fact]
    public void Despues_requests_are_checked()
    {
        var scenario = Make("""{"status":200,"despues":[{"metodo":"GET","ruta":"/api/salud","status":200}]}""");

        Assert.True(Checker.Check(scenario, Observe(200, Ok200, null, 200), CheckSite.Tests).Passed);
        Assert.False(Checker.Check(scenario, Observe(200, Ok200, null, 500), CheckSite.Tests).Passed);
        Assert.False(Checker.Check(scenario, Observe(200, Ok200), CheckSite.Tests).Passed);
    }

    [Fact]
    public void A_scenario_whose_fields_are_only_for_evals_reports_zero_checks_in_tests()
    {
        var scenario = Make("""{"respuesta":{"contiene":["3.0"]},"tokens":">0"}""");

        var outcome = Checker.Check(scenario, Observe(200, Ok200), CheckSite.Tests);

        Assert.Equal(0, outcome.ChecksRun);
    }
}
