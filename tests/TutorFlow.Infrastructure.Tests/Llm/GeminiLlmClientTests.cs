using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Ports.Llm;
using TutorFlow.Infrastructure.Llm;

namespace TutorFlow.Infrastructure.Tests.Llm;

/// <summary>Real LLM adapter against a fake <see cref="HttpMessageHandler"/>: no network (R-QA-03, R-QA-04, R-RES-05, CL-23).</summary>
public sealed class GeminiLlmClientTests
{
    private const string Key = "AIza-secret-key-for-tests";

    private sealed class StubHandler(Func<HttpRequestMessage, string?, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await respond(request, Body, cancellationToken);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static (GeminiLlmClient Client, StubHandler Handler) Create(
        Func<HttpRequestMessage, string?, CancellationToken, Task<HttpResponseMessage>> respond,
        TimeSpan? timeout = null,
        int maxRetries = 2,
        TimeSpan? backoff = null,
        Action<LlmOptions>? configure = null)
    {
        var handler = new StubHandler(respond);
        var options = new LlmOptions
        {
            ApiKey = Key,
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
            MaxRetries = maxRetries,
            RetryBackoff = backoff ?? TimeSpan.FromMilliseconds(1),
        };
        configure?.Invoke(options);
        var http = new HttpClient(handler) { BaseAddress = options.BaseUrl };
        return (new GeminiLlmClient(http, Options.Create(options), NullLogger<GeminiLlmClient>.Instance), handler);
    }

    private static (GeminiLlmClient Client, StubHandler Handler) CreateReturning(HttpStatusCode status, string json) =>
        Create((_, _, _) => Task.FromResult(Json(status, json)));

    private static LlmRequest SampleRequest(JsonElement? schema = null)
    {
        var tool = new LlmToolDefinition(
            "consultar_notas",
            "Returns the grades of the user.",
            JsonElement.Parse("""{"type":"object","properties":{"periodo":{"type":"string"}}}"""));
        return new LlmRequest("Eres un tutor.", [LlmMessage.User("hola")], [tool], ResponseSchema: schema);
    }

    private const string TextReply = """
        {"candidates":[{"content":{"role":"model","parts":[{"text":"Hola"}]},"finishReason":"STOP"}],
         "usageMetadata":{"promptTokenCount":1000,"candidatesTokenCount":100,"thoughtsTokenCount":50,"totalTokenCount":1150}}
        """;

    [Fact]
    public async Task Request_UsesTheModelEndpointAndKeyHeaderNeverTheUrl()
    {
        var (client, handler) = CreateReturning(HttpStatusCode.OK, TextReply);

        await client.CompleteAsync(SampleRequest(), CancellationToken.None);

        var uri = handler.Request!.RequestUri!;
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent",
            uri.ToString());
        Assert.DoesNotContain(Key, uri.ToString(), StringComparison.Ordinal);
        Assert.Equal(Key, Assert.Single(handler.Request.Headers.GetValues("x-goog-api-key")));
        Assert.DoesNotContain(Key, handler.Body!, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
    }

    [Fact]
    public async Task Request_SerializesTemperatureZeroSystemInstructionAndTools()
    {
        var (client, handler) = CreateReturning(HttpStatusCode.OK, TextReply);

        await client.CompleteAsync(SampleRequest(), CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.Body!);
        var root = doc.RootElement;
        Assert.Equal(0, root.GetProperty("generationConfig").GetProperty("temperature").GetDouble());
        Assert.False(root.GetProperty("generationConfig").TryGetProperty("responseMimeType", out _));
        Assert.Equal("Eres un tutor.", root.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        var content = root.GetProperty("contents")[0];
        Assert.Equal("user", content.GetProperty("role").GetString());
        Assert.Equal("hola", content.GetProperty("parts")[0].GetProperty("text").GetString());
        var declaration = root.GetProperty("tools")[0].GetProperty("functionDeclarations")[0];
        Assert.Equal("consultar_notas", declaration.GetProperty("name").GetString());
        Assert.Equal("string", declaration.GetProperty("parametersJsonSchema")
            .GetProperty("properties").GetProperty("periodo").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Request_SendsLowThinkingLevelByDefault()
    {
        var (client, handler) = CreateReturning(HttpStatusCode.OK, TextReply);

        await client.CompleteAsync(SampleRequest(), CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.Body!);
        var generation = doc.RootElement.GetProperty("generationConfig");
        Assert.Equal("low", generation.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
        Assert.Equal(0, generation.GetProperty("temperature").GetDouble());
    }

    [Fact]
    public async Task Request_SendsTheConfiguredThinkingLevel()
    {
        var (client, handler) = Create(
            (_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, TextReply)), configure: o => o.ThinkingLevel = "High");

        await client.CompleteAsync(SampleRequest(), CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.Body!);
        Assert.Equal("high", doc.RootElement.GetProperty("generationConfig")
            .GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
    }

    [Theory]
    [InlineData("gemini-3.1-flash-lite", "")]
    [InlineData("gemini-2.5-flash", "low")]
    public async Task Request_OmitsThinkingConfigWhenDisabledOrModelIsNotGemini3(string model, string level)
    {
        var (client, handler) = Create(
            (_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, TextReply)),
            configure: o => { o.Model = model; o.ThinkingLevel = level; });

        await client.CompleteAsync(SampleRequest(), CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.Body!);
        Assert.False(doc.RootElement.GetProperty("generationConfig").TryGetProperty("thinkingConfig", out _));
    }

    [Fact]
    public async Task Request_WithResponseSchema_AsksForStructuredJson()
    {
        var (client, handler) = CreateReturning(HttpStatusCode.OK, TextReply);
        var schema = JsonElement.Parse("""{"type":"object","properties":{"respuesta":{"type":"string"}}}""");

        await client.CompleteAsync(SampleRequest(schema), CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.Body!);
        var generation = doc.RootElement.GetProperty("generationConfig");
        Assert.Equal("application/json", generation.GetProperty("responseMimeType").GetString());
        Assert.Equal("object", generation.GetProperty("responseJsonSchema").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Request_EchoesToolCallsWithThoughtSignatureAndToolResults()
    {
        var (client, handler) = CreateReturning(HttpStatusCode.OK, TextReply);
        var args = JsonElement.Parse("""{"periodo":"2025-1"}""");
        var result = JsonElement.Parse("""{"notas":[4.5]}""");
        var request = new LlmRequest(
            "sys",
            [
                LlmMessage.User("mis notas"),
                LlmMessage.Model(null, [new LlmToolCall("consultar_notas", args, "c1", "SIG-123")]),
                LlmMessage.Tool([new LlmToolResult("consultar_notas", result, "c1")]),
            ],
            []);

        await client.CompleteAsync(request, CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.Body!);
        Assert.False(doc.RootElement.TryGetProperty("tools", out _));
        var contents = doc.RootElement.GetProperty("contents");
        var modelPart = contents[1].GetProperty("parts")[0];
        Assert.Equal("model", contents[1].GetProperty("role").GetString());
        Assert.Equal("SIG-123", modelPart.GetProperty("thoughtSignature").GetString());
        Assert.Equal("2025-1", modelPart.GetProperty("functionCall").GetProperty("args").GetProperty("periodo").GetString());
        var response = contents[2].GetProperty("parts")[0].GetProperty("functionResponse");
        Assert.Equal("user", contents[2].GetProperty("role").GetString());
        Assert.Equal("consultar_notas", response.GetProperty("name").GetString());
        Assert.Equal("c1", response.GetProperty("id").GetString());
        Assert.Equal(4.5, response.GetProperty("response").GetProperty("notas")[0].GetDouble());
    }

    [Fact]
    public async Task Response_ParsesTextTokensAndCost()
    {
        var (client, _) = CreateReturning(HttpStatusCode.OK, TextReply);

        var response = await client.CompleteAsync(SampleRequest(), CancellationToken.None);

        Assert.Equal("Hola", response.Text);
        Assert.Empty(response.ToolCalls);
        Assert.Equal(1000, response.Usage.PromptTokens);
        Assert.Equal(100, response.Usage.CandidatesTokens);
        Assert.Equal(50, response.Usage.ThoughtsTokens);
        Assert.Equal(1150, response.Usage.TotalTokens);
        // (1000 * 0.25 + 150 * 1.50) / 1e6
        Assert.Equal(0.000475m, response.Usage.CostUsd);
    }

    [Fact]
    public async Task Response_WithoutThoughtsTokens_CountsZero()
    {
        var (client, _) = CreateReturning(HttpStatusCode.OK, """
            {"candidates":[{"content":{"parts":[{"text":"ok"}]}}],
             "usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5,"totalTokenCount":15}}
            """);

        var response = await client.CompleteAsync(SampleRequest(), CancellationToken.None);

        Assert.Equal(0, response.Usage.ThoughtsTokens);
        Assert.Equal(15, response.Usage.TotalTokens);
    }

    [Fact]
    public async Task Response_ParsesFunctionCallWithSignatureAndSkipsThoughtText()
    {
        var (client, _) = CreateReturning(HttpStatusCode.OK, """
            {"candidates":[{"content":{"role":"model","parts":[
               {"text":"thinking...","thought":true},
               {"functionCall":{"name":"rechazar","args":{"motivo":"fuera de alcance"},"id":"x9"},"thoughtSignature":"SIG-A"},
               {"functionCall":{"name":"consultar_notas"}}]}}],
             "usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5,"totalTokenCount":15}}
            """);

        var response = await client.CompleteAsync(SampleRequest(), CancellationToken.None);

        Assert.Null(response.Text);
        Assert.Equal(2, response.ToolCalls.Count);
        var first = response.ToolCalls[0];
        Assert.Equal("rechazar", first.Name);
        Assert.Equal("fuera de alcance", first.Arguments.GetProperty("motivo").GetString());
        Assert.Equal("x9", first.Id);
        Assert.Equal("SIG-A", first.ProviderState);
        Assert.Equal(JsonValueKind.Object, response.ToolCalls[1].Arguments.ValueKind);
        Assert.Null(response.ToolCalls[1].ProviderState);
    }

    [Theory]
    [InlineData("""{"candidates":[]}""")]
    [InlineData("""{"promptFeedback":{"blockReason":"SAFETY"}}""")]
    [InlineData("not json")]
    public async Task UnusableReply_ThrowsDependencyUnavailable(string body)
    {
        var (client, _) = CreateReturning(HttpStatusCode.OK, body);

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => client.CompleteAsync(SampleRequest(), CancellationToken.None));

        Assert.Equal(ExternalDependency.Llm, ex.Dependency);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task QuotaAndServerErrors_ThrowDependencyUnavailable(HttpStatusCode status)
    {
        var (client, _) = CreateReturning(status, """{"error":{"code":1,"message":"quota"}}""");

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => client.CompleteAsync(SampleRequest(), CancellationToken.None));

        Assert.Equal(ExternalDependency.Llm, ex.Dependency);
    }

    [Fact]
    public async Task BadRequest_GivesAClearMessageWithoutTheKey()
    {
        var (client, _) = CreateReturning(
            HttpStatusCode.BadRequest,
            $$$"""{"error":{"code":400,"message":"API key not valid: {{{Key}}}. Please pass a valid API key."}}""");

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => client.CompleteAsync(SampleRequest(), CancellationToken.None));

        Assert.Contains("HTTP 400", ex.Message, StringComparison.Ordinal);
        Assert.Contains("API key not valid", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Timeout_ThrowsDependencyUnavailable()
    {
        var (client, _) = Create(
            async (_, _, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return Json(HttpStatusCode.OK, TextReply);
            },
            timeout: TimeSpan.FromMilliseconds(100));

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => client.CompleteAsync(SampleRequest(), CancellationToken.None));

        Assert.Equal(ExternalDependency.Llm, ex.Dependency);
        Assert.Contains("did not answer", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellation_IsNotReportedAsTimeout()
    {
        using var cts = new CancellationTokenSource();
        var (client, _) = Create(async (_, _, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return Json(HttpStatusCode.OK, TextReply);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.CompleteAsync(SampleRequest(), cts.Token));
    }

    [Fact]
    public async Task NetworkError_ThrowsDependencyUnavailableWithoutTheKey()
    {
        var (client, _) = Create((_, _, _) => throw new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => client.CompleteAsync(SampleRequest(), CancellationToken.None));

        Assert.Equal(ExternalDependency.Llm, ex.Dependency);
        Assert.DoesNotContain(Key, ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ErrorMessages_NeverContainTheKeyOrThePrompt()
    {
        foreach (var status in new[] { HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError, HttpStatusCode.Forbidden })
        {
            var (client, _) = CreateReturning(status, $$$"""{"error":{"message":"bad key {{{Key}}}"}}""");
            var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(
                () => client.CompleteAsync(SampleRequest(), CancellationToken.None));
            Assert.DoesNotContain(Key, ex.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("hola", ex.Message, StringComparison.Ordinal);
        }
    }

    private const string Overload = """{"error":{"code":503,"message":"This model is currently experiencing high demand.","status":"UNAVAILABLE"}}""";

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task TransientError_IsRetriedAndSucceeds(HttpStatusCode status)
    {
        var calls = 0;
        var (client, _) = Create((_, _, _) =>
            Task.FromResult(++calls < 3 ? Json(status, Overload) : Json(HttpStatusCode.OK, TextReply)));

        var response = await client.CompleteAsync(SampleRequest(), CancellationToken.None);

        Assert.Equal(3, calls);
        Assert.Equal("Hola", response.Text);
        Assert.Equal(1150, response.Usage.TotalTokens); // Only the successful attempt reports tokens (R-RES-05).
    }

    [Fact]
    public async Task PersistentTransientError_ThrowsAfterTheConfiguredRetries()
    {
        var calls = 0;
        var (client, _) = Create((_, _, _) =>
        {
            calls++;
            return Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, Overload));
        });

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => client.CompleteAsync(SampleRequest(), CancellationToken.None));

        Assert.Equal(ExternalDependency.Llm, ex.Dependency);
        Assert.Equal(3, calls); // 1 attempt + 2 retries.
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task NonTransientError_IsNotRetried(HttpStatusCode status)
    {
        var calls = 0;
        var (client, _) = Create((_, _, _) =>
        {
            calls++;
            return Task.FromResult(Json(status, """{"error":{"message":"x"}}"""));
        });

        await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => client.CompleteAsync(SampleRequest(), CancellationToken.None));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Retries_Disabled_MakesASingleCall()
    {
        var calls = 0;
        var (client, _) = Create(
            (_, _, _) =>
            {
                calls++;
                return Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, Overload));
            },
            maxRetries: 0);

        await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => client.CompleteAsync(SampleRequest(), CancellationToken.None));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Retry_IsSkippedWhenTheBackoffDoesNotFitInTheTimeBudget()
    {
        var calls = 0;
        var (client, _) = Create(
            (_, _, _) =>
            {
                calls++;
                return Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, Overload));
            },
            timeout: TimeSpan.FromMilliseconds(300),
            backoff: TimeSpan.FromSeconds(5));

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => client.CompleteAsync(SampleRequest(), CancellationToken.None));

        Assert.Equal(1, calls);
        Assert.Contains("HTTP 503", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TotalTimeout_IncludesTheRetries()
    {
        var calls = 0;
        var (client, _) = Create(
            async (_, _, ct) =>
            {
                calls++;
                await Task.Delay(TimeSpan.FromMilliseconds(80), ct);
                return Json(HttpStatusCode.ServiceUnavailable, Overload);
            },
            timeout: TimeSpan.FromMilliseconds(300),
            maxRetries: 50,
            backoff: TimeSpan.FromMilliseconds(1));

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => client.CompleteAsync(SampleRequest(), CancellationToken.None));

        Assert.True(calls < 50);
        Assert.Equal(ExternalDependency.Llm, ex.Dependency);
    }

    [Fact]
    public async Task CallerCancellation_DuringBackoff_IsNotRetriedNorMappedTo503()
    {
        using var cts = new CancellationTokenSource();
        var calls = 0;
        var (client, _) = Create(
            (_, _, _) =>
            {
                calls++;
                cts.Cancel();
                return Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, Overload));
            },
            backoff: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.CompleteAsync(SampleRequest(), cts.Token));

        Assert.Equal(1, calls);
    }
}
