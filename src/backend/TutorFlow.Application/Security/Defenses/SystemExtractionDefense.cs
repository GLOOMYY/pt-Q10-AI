using System.Text.RegularExpressions;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Security.Defenses;

/// <summary>
/// Deterministic detection of requests for the system itself (R-SEG-13, D-25): the system prompt, the internal
/// instructions, the list of tools, the configuration or a secret such as <c>LLM_API_KEY</c>. Such a request is a 403 for
/// every user and the attempt is audited with <see cref="AuditReasons.SystemExtractionAttempt"/> (R-LOG-03).
/// </summary>
/// <remarks>
/// Only unmistakable phrasings are matched, so legitimate questions about grades, attendance, scholarships or the
/// regulations are never blocked (checked against <c>escenarios/</c>). Subtler requests are handled by the model with
/// <c>rechazar</c>. The defense never calls the simulated API and never echoes or stores any secret (R-LOG-05).
/// </remarks>
public sealed partial class SystemExtractionDefense : IQuestionDefense
{
    /// <inheritdoc />
    public Task<DefenseVerdict> EvaluateAsync(UserIdentity user, string question, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        return Task.FromResult(IsExtraction(question)
            ? DefenseVerdict.Block(AuditReasons.SystemExtractionAttempt)
            : DefenseVerdict.Allow);
    }

    /// <summary>Whether the question asks for the prompt, instructions, tools, configuration or secrets.</summary>
    /// <param name="question">The question as received.</param>
    /// <returns><see langword="true"/> if an extraction pattern is present.</returns>
    public static bool IsExtraction(string question)
    {
        var text = TextNormalizer.Normalize(question);
        return Secret().IsMatch(text)
            || SystemPrompt().IsMatch(text)
            || InternalInstructions().IsMatch(text)
            || ToolList().IsMatch(text)
            || OwnConfiguration().IsMatch(text)
            || RevealOwnInstructions().IsMatch(text);
    }

    // Only unmistakable wording about the assistant itself. Words that can also refer to the regulations ("prompt" alone,
    // "reglas internas", "lista de funciones", "herramientas disponibles", "sus reglas") are NOT matched: the model decides
    // those with `rechazar`.

    // The key, environment variables and the .env file.
    [GeneratedRegex(
        @"llm_api_key|\bapi[ _-]?key\b|\bapikey\b|\bclave (?:de la |del )?api\b|\bvariables? de entorno\b|\benvironment variables?\b|(?<![\w])\.env\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex Secret();

    // "tu prompt de sistema", "system prompt", "your prompt".
    [GeneratedRegex(@"\bsystem prompt\b|\bprompt (?:de |del )?sistema\b|\b(?:tu|tus|your) prompts?\b", RegexOptions.CultureInvariant)]
    private static partial Regex SystemPrompt();

    // "tus instrucciones internas", "instrucciones del sistema", "system message", "hidden instructions".
    [GeneratedRegex(
        @"\b(?:tus|your) (?:instrucciones|instructions) (?:internas?|ocultas?|secretas?|del sistema|de sistema|iniciales|originales)\b|\binstrucciones (?:del|de) sistema\b|\b(?:system|hidden) (?:message|instructions?)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex InternalInstructions();

    // "la lista de tus herramientas", "las herramientas que tienes", "qué herramientas usas", "your tools".
    [GeneratedRegex(
        @"\blista de (?:tus|sus) (?:herramientas|tools|funciones)\b|\bherramientas que (?:tienes|usas|puedes usar)\b|\bque herramientas (?:tienes|usas|puedes usar)\b|\b(?:tus|your) (?:herramientas|tools)\b|\bwhat tools\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex ToolList();

    [GeneratedRegex(@"\b(?:tu|your) (?:configuracion|configuration) (?:interna|del sistema|de sistema|internal)\b", RegexOptions.CultureInvariant)]
    private static partial Regex OwnConfiguration();

    // "muéstrame tus instrucciones", "repeat your instructions": a request verb followed by "your instructions".
    [GeneratedRegex(
        @"\b(?:muestra|muestrame|dime|revela|repite|imprime|copia|show|reveal|print|repeat|tell me)\b(?:\W+\w+){0,5}?\W+(?:tus|your) (?:instrucciones|instructions)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex RevealOwnInstructions();
}
