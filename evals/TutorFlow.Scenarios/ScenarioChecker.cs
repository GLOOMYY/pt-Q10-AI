using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TutorFlow.Scenarios;

/// <summary>Where a scenario runs: decides which fields of the expected output are checked (README section 4.4).</summary>
public enum CheckSite
{
    /// <summary><c>tests/</c>: fake LLM, so nothing that depends on the text or on the real tokens is checked.</summary>
    Tests,

    /// <summary><c>evals/</c>: real LLM, everything is checked.</summary>
    Evals,
}

/// <summary>What was observed when a scenario was executed.</summary>
/// <param name="Status">HTTP status of the main request.</param>
/// <param name="Body">Response body as text.</param>
/// <param name="AuditRecord">The audit record of the request (one JSON object), or <see langword="null"/> if not found.</param>
/// <param name="AfterStatuses">Statuses of the <c>despues</c> requests, in order.</param>
public sealed record ObservedResult(
    int Status,
    string Body,
    JsonElement? AuditRecord,
    IReadOnlyList<int> AfterStatuses);

/// <summary>Outcome of checking one scenario at one site.</summary>
/// <param name="Failures">One message per failed check; empty if all passed.</param>
/// <param name="ChecksRun">How many checks applied at this site. Zero means the scenario has nothing to verify there.</param>
public sealed record CheckOutcome(IReadOnlyList<string> Failures, int ChecksRun)
{
    /// <summary>Whether no check failed.</summary>
    public bool Passed => Failures.Count == 0;
}

/// <summary>
/// Applies the <c>Output esperado</c> of a scenario to what was observed, following README sections 4 and 5 and the
/// table of section 4.4 (which fields each site checks). Only what appears in the block is evaluated.
/// </summary>
public sealed class ScenarioChecker
{
    private static readonly JsonSerializerOptions Unescaped = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string[] NumericLogFields = ["tokens", "costoUsd", "duracionMs"];

    private readonly GlobalCitationRules _globalCitations;

    /// <summary>Creates the checker.</summary>
    /// <param name="globalCitations">The global list of allowed citations (README section 4.3).</param>
    public ScenarioChecker(GlobalCitationRules globalCitations) => _globalCitations = globalCitations;

    /// <summary>Checks one scenario.</summary>
    /// <param name="scenario">An <c>api</c> scenario.</param>
    /// <param name="observed">What was observed.</param>
    /// <param name="site">Where it was executed.</param>
    /// <returns>The failures and how many checks applied.</returns>
    public CheckOutcome Check(Scenario scenario, ObservedResult observed, CheckSite site)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(observed);
        var run = new Run(scenario, observed, site, _globalCitations);
        run.Execute();
        return new CheckOutcome(run.Failures, run.Checks);
    }

    private sealed class Run(Scenario scenario, ObservedResult observed, CheckSite site, GlobalCitationRules global)
    {
        private readonly JsonElement _expected = scenario.Expected;
        private JsonElement? _body;
        private bool _bodyParsed;

        public List<string> Failures { get; } = [];

        public int Checks { get; private set; }

        private bool IsEvals => site == CheckSite.Evals;

        public void Execute()
        {
            CheckStatus();
            CheckAnswerText();
            CheckCitations();
            CheckUsedStudentData();
            CheckNumber("tokens");
            CheckNumber("costoUsd");
            CheckLog();
            CheckAfter();
        }

        private void Fail(string message) => Failures.Add(message);

        private JsonElement? Body()
        {
            if (!_bodyParsed)
            {
                _bodyParsed = true;
                try
                {
                    using var document = JsonDocument.Parse(observed.Body);
                    _body = document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
                }
                catch (JsonException)
                {
                    _body = null;
                }
            }

            return _body;
        }

        private void CheckStatus()
        {
            if (!_expected.TryGetProperty("status", out var status))
            {
                return;
            }

            Checks++;
            if (observed.Status != status.GetInt32())
            {
                Fail($"status: expected {status.GetInt32()} but got {observed.Status}.");
            }
        }

        private void CheckAnswerText()
        {
            if (!_expected.TryGetProperty("respuesta", out var answerRules))
            {
                return;
            }

            string? answer = null;
            var hasAnswer = Body() is { } body && body.TryGetProperty("respuesta", out var a) && a.ValueKind == JsonValueKind.String
                && (answer = a.GetString()) is not null;

            if (answerRules.TryGetProperty("igual", out var equal))
            {
                Checks++;
                if (!hasAnswer || !string.Equals(answer, equal.GetString(), StringComparison.Ordinal))
                {
                    Fail($"respuesta.igual: expected \"{equal.GetString()}\" but got \"{(hasAnswer ? answer : "<no respuesta field>")}\".");
                }
            }

            if (!IsEvals)
            {
                return; // README 4.4: respuesta.contiene / noContiene only in evals/.
            }

            if (answerRules.TryGetProperty("contiene", out var contains))
            {
                foreach (var fragment in contains.EnumerateArray().Select(e => e.GetString()!))
                {
                    Checks++;
                    if (!hasAnswer || !TextMatching.Contains(answer!, fragment))
                    {
                        Fail($"respuesta.contiene: \"{fragment}\" does not appear in the answer.");
                    }
                }
            }

            if (answerRules.TryGetProperty("noContiene", out var notContains))
            {
                foreach (var fragment in notContains.EnumerateArray().Select(e => e.GetString()!))
                {
                    Checks++;
                    if (hasAnswer && TextMatching.Contains(answer!, fragment))
                    {
                        Fail($"respuesta.noContiene: \"{fragment}\" appears in the answer.");
                    }
                }
            }
        }

        private List<ObservedCitation>? ReadCitations()
        {
            if (Body() is not { } body || !body.TryGetProperty("citas", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new List<ObservedCitation>();
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("documento", out var document) && document.ValueKind == JsonValueKind.String
                    && item.TryGetProperty("seccion", out var section) && section.ValueKind == JsonValueKind.String)
                {
                    result.Add(new ObservedCitation(document.GetString()!, section.GetString()!));
                }
                else
                {
                    return null;
                }
            }

            return result;
        }

        private void CheckCitations()
        {
            // README 4.4: citas of 400/403/503 in both sites; of 200 only in evals/. Same for the global check (4.3).
            var errorStatus = observed.Status is 400 or 403 or 503;
            if (!errorStatus && !(IsEvals && observed.Status == 200))
            {
                return;
            }

            var citations = ReadCitations();
            if (_expected.TryGetProperty("citas", out var rules))
            {
                CheckCitationRules(rules, citations);
            }

            Checks++;
            if (citations is null)
            {
                Fail("citas (global): the response has no valid 'citas' list.");
                return;
            }

            foreach (var citation in citations.Where(c => !global.IsAllowed(c)))
            {
                Fail($"citas (global, README 4.3): the citation '{citation}' is not allowed.");
            }
        }

        private void CheckCitationRules(JsonElement rules, List<ObservedCitation>? citations)
        {
            if (rules.TryGetProperty("exactas", out var exact))
            {
                Checks++;
                var expected = ReadList(exact);
                if (citations is null || !SameMultiset(expected, citations))
                {
                    Fail($"citas.exactas: expected [{string.Join("; ", expected)}] but got [{(citations is null ? "<none>" : string.Join("; ", citations))}].");
                }
            }

            if (rules.TryGetProperty("incluye", out var include))
            {
                foreach (var citation in ReadList(include))
                {
                    Checks++;
                    if (citations is null || !citations.Contains(citation))
                    {
                        Fail($"citas.incluye: the citation '{citation}' is missing.");
                    }
                }
            }

            if (rules.TryGetProperty("excluye", out var exclude))
            {
                foreach (var citation in ReadList(exclude))
                {
                    Checks++;
                    if (citations is not null && citations.Contains(citation))
                    {
                        Fail($"citas.excluye: the citation '{citation}' must not appear.");
                    }
                }
            }

            if (rules.TryGetProperty("vacias", out var empty) && empty.ValueKind == JsonValueKind.True)
            {
                Checks++;
                if (citations is null || citations.Count != 0)
                {
                    Fail($"citas.vacias: expected [] but got [{(citations is null ? "<none>" : string.Join("; ", citations))}].");
                }
            }
        }

        private static List<ObservedCitation> ReadList(JsonElement list) =>
            [.. list.EnumerateArray().Select(e => new ObservedCitation(e.GetProperty("documento").GetString()!, e.GetProperty("seccion").GetString()!))];

        private static bool SameMultiset(List<ObservedCitation> expected, List<ObservedCitation> actual) =>
            expected.Count == actual.Count
            && expected.GroupBy(c => c).All(g => actual.Count(c => c == g.Key) == g.Count());

        private void CheckUsedStudentData()
        {
            if (!_expected.TryGetProperty("usoDatosEstudiante", out var expected))
            {
                return;
            }

            Checks++;
            if (Body() is not { } body || !body.TryGetProperty("usoDatosEstudiante", out var actual)
                || actual.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || actual.GetBoolean() != expected.GetBoolean())
            {
                Fail($"usoDatosEstudiante: expected {expected.GetBoolean().ToString().ToLowerInvariant()} but got {Describe(Body(), "usoDatosEstudiante")}.");
            }
        }

        private void CheckNumber(string name)
        {
            if (!_expected.TryGetProperty(name, out var expected))
            {
                return;
            }

            var isGreaterThanZero = expected.ValueKind == JsonValueKind.String && expected.GetString() == ">0";
            if (isGreaterThanZero && !IsEvals)
            {
                return; // README 4.4: ">0" only in evals/.
            }

            if (!isGreaterThanZero && expected.ValueKind != JsonValueKind.Number)
            {
                Fail($"{name}: unsupported expectation {expected.GetRawText()}.");
                return;
            }

            // README 4.4: an exact value (0) is checked in both sites.
            Checks++;
            if (Body() is not { } body || !body.TryGetProperty(name, out var actual) || actual.ValueKind != JsonValueKind.Number)
            {
                Fail($"{name}: the response has no numeric '{name}'.");
                return;
            }

            var value = actual.GetDecimal();
            if (isGreaterThanZero ? value <= 0 : value != expected.GetDecimal())
            {
                Fail($"{name}: expected {(isGreaterThanZero ? ">0" : expected.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture))} but got {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
            }
        }

        private void CheckLog()
        {
            if (!_expected.TryGetProperty("log", out var rules))
            {
                return;
            }

            if (observed.AuditRecord is not { } record)
            {
                Checks++;
                Fail("log: the audit record of the request was not found (exactly one is expected, R-LOG-01).");
                return;
            }

            if (rules.TryGetProperty("estudiantesConsultados", out var students) && ChecksStudentsConsulted())
            {
                Checks++;
                var expected = students.EnumerateArray().Select(e => e.GetString()!).Order(StringComparer.Ordinal).ToList();
                var actual = record.TryGetProperty("estudiantesConsultados", out var list) && list.ValueKind == JsonValueKind.Array
                    ? list.EnumerateArray().Select(e => e.GetString() ?? string.Empty).Order(StringComparer.Ordinal).ToList()
                    : null;
                if (actual is null || !expected.SequenceEqual(actual))
                {
                    Fail($"log.estudiantesConsultados: expected [{string.Join(", ", expected)}] but got [{(actual is null ? "<none>" : string.Join(", ", actual))}].");
                }
            }

            if (rules.TryGetProperty("status", out var status))
            {
                Checks++;
                if (!record.TryGetProperty("status", out var actual) || actual.ValueKind != JsonValueKind.Number || actual.GetInt32() != status.GetInt32())
                {
                    Fail($"log.status: expected {status.GetInt32()} but got {Describe(record, "status")}.");
                }
            }

            if (rules.TryGetProperty("motivo", out var reason))
            {
                Checks++;
                var actual = record.TryGetProperty("motivo", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                if (!string.Equals(actual, reason.GetString(), StringComparison.Ordinal))
                {
                    Fail($"log.motivo: expected \"{reason.GetString()}\" but got {(actual is null ? "null" : $"\"{actual}\"")}.");
                }
            }

            if (rules.TryGetProperty("noContiene", out var forbidden))
            {
                var text = SerializeForNotContains(record);
                foreach (var fragment in forbidden.EnumerateArray().Select(e => e.GetString()!))
                {
                    Checks++;
                    if (TextMatching.Contains(text, fragment))
                    {
                        Fail($"log.noContiene: \"{fragment}\" appears in the audit record.");
                    }
                }
            }
        }

        // README 4.4 / D-32: in evals/, log.estudiantesConsultados of a 403 resolved by `rechazar` (scenarios that verify
        // R-ARQ-04) is not checked, because the real model may query data before rejecting.
        private bool ChecksStudentsConsulted() =>
            !(IsEvals && observed.Status == 403 && scenario.Verifying("R-ARQ-04"));

        // README 5.3: the record without tokens, costoUsd, duracionMs and pregunta, serialized without escaping non-ASCII.
        private static string SerializeForNotContains(JsonElement record)
        {
            var node = JsonNode.Parse(record.GetRawText())!.AsObject();
            foreach (var field in NumericLogFields.Append("pregunta"))
            {
                node.Remove(field);
            }

            return node.ToJsonString(Unescaped);
        }

        private void CheckAfter()
        {
            if (!_expected.TryGetProperty("despues", out var after))
            {
                return;
            }

            var requests = after.EnumerateArray().ToList();
            for (var i = 0; i < requests.Count; i++)
            {
                Checks++;
                var expected = requests[i].GetProperty("status").GetInt32();
                var label = $"despues[{i}] {requests[i].GetProperty("metodo").GetString()} {requests[i].GetProperty("ruta").GetString()}";
                if (i >= observed.AfterStatuses.Count)
                {
                    Fail($"{label}: the request was not made.");
                }
                else if (observed.AfterStatuses[i] != expected)
                {
                    Fail($"{label}: expected {expected} but got {observed.AfterStatuses[i]}.");
                }
            }
        }

        private static string Describe(JsonElement? element, string property) =>
            element is { } e && e.TryGetProperty(property, out var value) ? value.GetRawText() : "<missing>";
    }
}
