using System.Text.Json;
using TutorFlow.Api.Contracts;
using TutorFlow.Application.Assistant;

namespace TutorFlow.Api.Endpoints;

/// <summary>Inbound adapter of <c>POST /api/asistente/preguntar</c> (SPEC §5).</summary>
public static class AssistantEndpoint
{
    /// <summary>Route of the endpoint.</summary>
    public const string Route = "/api/asistente/preguntar";

    // Strict reading: case-sensitive names, no comments, no trailing commas, no numbers as strings.
    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.General);

    /// <summary>Maps <c>POST /api/asistente/preguntar</c>. <c>Program.cs</c> invokes it next to <c>MapHealth()</c>.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapAssistant(this IEndpointRouteBuilder app)
    {
        app.MapPost(Route, HandleAsync);
        return app;
    }

    /// <summary>Maps the use case outcome to the HTTP status of the contract (SPEC §5, §5.0).</summary>
    /// <param name="outcome">The use case outcome.</param>
    /// <returns>The HTTP status code.</returns>
    public static int ToStatusCode(AssistantOutcome outcome) => outcome switch
    {
        AssistantOutcome.Answered => StatusCodes.Status200OK,
        AssistantOutcome.InvalidRequest => StatusCodes.Status400BadRequest,
        AssistantOutcome.Forbidden => StatusCodes.Status403Forbidden,
        AssistantOutcome.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown outcome."),
    };

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IAssistantService assistant,
        CancellationToken cancellationToken)
    {
        // The body is read by hand (not by minimal API binding) so that any malformed body, whatever its
        // Content-Type, still gets the 400 with the contract shape (SPEC §5.0) and goes through the use case.
        var input = await ReadInputAsync(request, cancellationToken).ConfigureAwait(false);
        var answer = await assistant.AskAsync(input, cancellationToken).ConfigureAwait(false);
        return Results.Json(AskQuestionResponse.From(answer), statusCode: ToStatusCode(answer.Outcome));
    }

    private static async Task<AskQuestionInput> ReadInputAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var body = await JsonSerializer
                .DeserializeAsync<AskQuestionRequest>(request.Body, RequestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return body?.ToInput() ?? AskQuestionInput.Malformed;
        }
        catch (JsonException)
        {
            // Invalid JSON, empty body or a field with the wrong JSON type (ESC-ENT-07, ESC-ENT-11).
            return AskQuestionInput.Malformed;
        }
    }
}
