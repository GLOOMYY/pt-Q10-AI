using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TutorFlow.Api.Middleware;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Audit;
using TutorFlow.Application.Ports.Audit;
using TutorFlow.Application.Ports.Clock;
using TutorFlow.Testing.Audit;
using Xunit;

namespace TutorFlow.Api.Tests.Middleware;

/// <summary>R-LOG-01, R-LOG-09, AC-03, D-30: one audit record per request, the real status, the correlation id.</summary>
public sealed class AuditTrailMiddlewareTests
{
    private const string Route = "/api/asistente/preguntar";

    private sealed class FixedClock : IClock
    {
        private DateTimeOffset _now = new(2026, 11, 26, 15, 0, 0, TimeSpan.Zero);

        // Every read advances 10 ms so the duration is deterministic.
        public DateTimeOffset UtcNow => _now = _now.AddMilliseconds(10);
    }

    private static async Task<(HttpClient Client, InMemoryAuditLog Log, WebApplication App)> StartAsync(
        Func<HttpContext, IRequestAuditContext, Task> endpoint)
    {
        var log = new InMemoryAuditLog();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAudit();
        builder.Services.AddSingleton<IAuditLog>(log);
        builder.Services.AddSingleton<IClock, FixedClock>();
        var app = builder.Build();
        app.UseAuditTrail();
        app.MapMethods(Route, ["POST", "GET"], (HttpContext http, IRequestAuditContext audit) => endpoint(http, audit));
        app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok" }));
        await app.StartAsync();
        return (app.GetTestClient(), log, app);
    }

    private static Task Respond(HttpContext http, IRequestAuditContext audit, AssistantAnswer answer, int status)
    {
        audit.SetRequest("¿Cuánto sacó Andrés?", new AuditUser("estudiante", "EST-001"));
        audit.Complete(answer);
        http.Response.StatusCode = status;
        return Task.CompletedTask;
    }

    private static HttpRequestMessage Post(string? correlationId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        if (correlationId is not null)
        {
            request.Headers.Add(CorrelationId.HeaderName, correlationId);
        }

        return request;
    }

    [Fact]
    public async Task Reuses_the_incoming_correlation_id_and_writes_exactly_one_record_R_LOG_09()
    {
        var (client, log, app) = await StartAsync((h, a) => Respond(h, a, AssistantAnswer.Forbidden(AuditReasons.DeterministicBarrier), 403));
        await using var _ = app;

        var response = await client.SendAsync(Post("eval-123"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("eval-123", Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)));
        var record = log.GetByCorrelationId("eval-123");
        Assert.Single(log.Records);
        Assert.Equal(403, record.Status);
        Assert.Equal("barrera determinista", record.Reason);
        Assert.Equal("¿Cuánto sacó Andrés?", record.Question);
        Assert.Equal(10, record.DurationMs);
    }

    [Fact]
    public async Task Generates_a_correlation_id_when_absent_or_unreasonable_and_it_matches_the_record()
    {
        var (client, log, app) = await StartAsync((h, a) => Respond(h, a, AssistantAnswer.Answered("ok", [], false, 5, 0.1m), 200));
        await using var _ = app;

        var withoutHeader = await client.SendAsync(Post());
        var tooLong = await client.SendAsync(Post(new string('x', 500)));

        foreach (var response in new[] { withoutHeader, tooLong })
        {
            var id = Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));
            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.Equal(200, log.GetByCorrelationId(id).Status);
        }

        Assert.Equal(2, log.Records.Count);
        Assert.NotEqual(
            withoutHeader.Headers.GetValues(CorrelationId.HeaderName).Single(),
            tooLong.Headers.GetValues(CorrelationId.HeaderName).Single());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(503)]
    public async Task Error_statuses_are_recorded_with_the_status_returned(int status)
    {
        var answer = status switch
        {
            400 => AssistantAnswer.InvalidRequest(),
            403 => AssistantAnswer.Forbidden(AuditReasons.ThirdPartyData),
            _ => AssistantAnswer.Unavailable(),
        };
        var (client, log, app) = await StartAsync((h, a) => Respond(h, a, answer, status));
        await using var _ = app;

        await client.SendAsync(Post("e-" + status));

        var record = log.GetByCorrelationId("e-" + status);
        Assert.Equal(status, record.Status);
        Assert.Contains(record.Reason, AuditReasons.All);
    }

    [Fact]
    public async Task Unhandled_exception_is_recorded_once_with_status_500()
    {
        var (client, log, app) = await StartAsync((_, _) => throw new InvalidOperationException("provider-quoted-secret"));
        await using var _ = app;

        // TestServer surfaces the exception to the caller; Kestrel would answer 500, which is what is recorded.
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(Post("boom-1")));
        var record = log.GetByCorrelationId("boom-1");
        Assert.Equal(500, record.Status);

        // R-LOG-02, R-LOG-05: no exception text (it may quote the request or the LLM provider's error) reaches the record.
        Assert.DoesNotContain("provider-quoted-secret", AuditJson.Serialize(record), StringComparison.Ordinal);
        Assert.Single(log.Records);
    }

    [Fact]
    public async Task Other_paths_get_the_header_but_no_audit_record()
    {
        var (client, log, app) = await StartAsync((h, a) => Respond(h, a, AssistantAnswer.Forbidden(AuditReasons.ThirdPartyData), 403));
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/salud");
        request.Headers.Add(CorrelationId.HeaderName, "health-1");
        var response = await client.SendAsync(request);

        Assert.Equal("health-1", Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)));
        Assert.Empty(log.Records);
    }

    [Fact]
    public async Task A_request_whose_body_was_never_read_still_gets_one_record_with_what_is_known()
    {
        // E.g. a 400 produced before the endpoint recorded the request: the record has no question nor user (R-LOG-01).
        var (client, log, app) = await StartAsync((h, a) =>
        {
            a.Complete(AssistantAnswer.InvalidRequest());
            h.Response.StatusCode = 400;
            return Task.CompletedTask;
        });
        await using var _ = app;

        await client.SendAsync(Post("bad-1"));

        var record = log.GetByCorrelationId("bad-1");
        Assert.Null(record.Question);
        Assert.Null(record.User.Id);
        Assert.Equal("solicitud inválida", record.Reason);
    }
}
