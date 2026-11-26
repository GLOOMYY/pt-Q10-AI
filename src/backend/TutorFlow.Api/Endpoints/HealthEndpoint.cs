namespace TutorFlow.Api.Endpoints;

/// <summary>Liveness endpoint used by the container healthcheck (SPEC section 5).</summary>
public static class HealthEndpoint
{
    /// <summary>Maps <c>GET /api/salud</c> returning <c>200 { "estado": "ok" }</c>.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok" }));
        return app;
    }
}
