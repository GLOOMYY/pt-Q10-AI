using System.Text.Json;

namespace TutorFlow.Scenarios;

/// <summary>Level of a scenario (escenarios/README section 2).</summary>
public enum ScenarioLevel
{
    /// <summary>HTTP request to the backend, with an LLM.</summary>
    Api,

    /// <summary>Calculation function of the backend, without LLM or network.</summary>
    Unit,
}

/// <summary>Closed list of preconditions (escenarios/README section 2.1).</summary>
public enum Precondition
{
    /// <summary><c>ninguna</c>: unit scenarios.</summary>
    None,

    /// <summary><c>api-simulada-levantada</c>.</summary>
    ApiUp,

    /// <summary><c>api-simulada-detenida</c>.</summary>
    ApiDown,

    /// <summary><c>llm-cuota-agotada</c>: only in <c>tests/</c>.</summary>
    LlmQuotaExhausted,
}

/// <summary>
/// A scenario read from <c>escenarios/*.md</c> following the grammar of README section 6. It is never copied nor
/// redefined: <see cref="Input"/> and <see cref="Expected"/> are the JSON blocks exactly as written in the file.
/// </summary>
/// <param name="Id">Scenario id, e.g. <c>ESC-BAN-01</c>.</param>
/// <param name="Title">Short title.</param>
/// <param name="File">Name of the file the scenario comes from, e.g. <c>banco.md</c>.</param>
/// <param name="Verifies">IDs of the SPEC the scenario verifies (the <c>Verifica</c> items).</param>
/// <param name="Level">Scenario level.</param>
/// <param name="Precondition">Scenario precondition.</param>
/// <param name="Input">The <c>Input</c> JSON block.</param>
/// <param name="Expected">The <c>Output esperado</c> JSON block.</param>
public sealed record Scenario(
    string Id,
    string Title,
    string File,
    IReadOnlyList<string> Verifies,
    ScenarioLevel Level,
    Precondition Precondition,
    JsonElement Input,
    JsonElement Expected)
{
    /// <summary>Whether the scenario verifies the given SPEC id exactly (e.g. <c>R-ARQ-04</c>).</summary>
    /// <param name="specId">SPEC id.</param>
    /// <returns><see langword="true"/> if listed in <see cref="Verifies"/>.</returns>
    public bool Verifying(string specId) => Verifies.Contains(specId, StringComparer.Ordinal);

    /// <inheritdoc />
    public override string ToString() => Id;
}

/// <summary>A scenario (or file) that does not follow the grammar: reported as a failure (README section 6).</summary>
/// <param name="Id">Scenario id, or the file name when the error is not tied to a scenario.</param>
/// <param name="File">File name.</param>
/// <param name="Message">What is wrong.</param>
public sealed record ScenarioFormatError(string Id, string File, string Message);

/// <summary>Result of reading the scenarios.</summary>
/// <param name="Scenarios">The scenarios read correctly, in file order.</param>
/// <param name="Errors">The format errors.</param>
/// <param name="Index">The index of README section 8: file name to (prefix, declared scenario count).</param>
public sealed record ScenarioLoadResult(
    IReadOnlyList<Scenario> Scenarios,
    IReadOnlyList<ScenarioFormatError> Errors,
    IReadOnlyDictionary<string, (string Prefix, int Count)> Index);
