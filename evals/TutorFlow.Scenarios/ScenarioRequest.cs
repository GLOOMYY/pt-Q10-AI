using System.Text.Json;
using System.Text.Json.Nodes;

namespace TutorFlow.Scenarios;

/// <summary>An HTTP request built from the <c>Input</c> block of a scenario (README section 3.1).</summary>
/// <param name="Method">HTTP method.</param>
/// <param name="Route">Route, e.g. <c>/api/asistente/preguntar</c>.</param>
/// <param name="Body">Exact body to send, or <see langword="null"/> when the scenario has no <c>cuerpo</c>.</param>
public sealed record ScenarioRequest(string Method, string Route, string? Body)
{
    /// <summary>
    /// Builds the request of a scenario. A <c>cuerpo</c> that is a string is sent as is (invalid JSON for the 400); any
    /// other is sent as its JSON text. A <c>&lt;generar: …&gt;</c> marker in <c>pregunta</c> is resolved with
    /// <see cref="GeneratedValues"/>; an unknown marker throws.
    /// </summary>
    /// <param name="scenario">An <c>api</c> scenario.</param>
    /// <returns>The request.</returns>
    /// <exception cref="InvalidOperationException">Unknown generation marker.</exception>
    public static ScenarioRequest From(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var input = scenario.Input;
        var method = input.GetProperty("metodo").GetString()!;
        var route = input.GetProperty("ruta").GetString()!;
        if (!input.TryGetProperty("cuerpo", out var body))
        {
            return new ScenarioRequest(method, route, null);
        }

        if (body.ValueKind == JsonValueKind.String)
        {
            return new ScenarioRequest(method, route, body.GetString());
        }

        if (body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty("pregunta", out var question)
            && question.ValueKind == JsonValueKind.String
            && GeneratedValues.IsMarker(question.GetString()!))
        {
            var node = JsonNode.Parse(body.GetRawText())!.AsObject();
            node["pregunta"] = GeneratedValues.Resolve(scenario.Id, question.GetString()!);
            return new ScenarioRequest(method, route, node.ToJsonString());
        }

        return new ScenarioRequest(method, route, body.GetRawText());
    }
}
