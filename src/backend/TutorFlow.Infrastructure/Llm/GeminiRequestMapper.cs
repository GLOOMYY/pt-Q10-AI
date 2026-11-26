using System.Text.Json;
using System.Text.Json.Nodes;
using TutorFlow.Application.Ports.Llm;

namespace TutorFlow.Infrastructure.Llm;

/// <summary>Maps an <see cref="LlmRequest"/> to the Gemini <c>generateContent</c> JSON body.</summary>
internal static class GeminiRequestMapper
{
    /// <summary>Builds the JSON body (system instruction, contents, tools and generation config).</summary>
    /// <param name="request">Provider-neutral request.</param>
    /// <returns>The JSON body.</returns>
    public static JsonObject ToJson(LlmRequest request)
    {
        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = request.SystemInstruction }),
            },
            ["contents"] = new JsonArray(request.Messages.Select(ToContent).ToArray<JsonNode?>()),
        };

        if (request.Tools.Count > 0)
        {
            body["tools"] = new JsonArray(new JsonObject
            {
                ["functionDeclarations"] = new JsonArray(request.Tools.Select(ToDeclaration).ToArray<JsonNode?>()),
            });
        }

        var generation = new JsonObject { ["temperature"] = request.Temperature };
        if (request.ResponseSchema is { } schema)
        {
            generation["responseMimeType"] = "application/json";
            generation["responseJsonSchema"] = Clone(schema);
        }

        body["generationConfig"] = generation;
        return body;
    }

    private static JsonObject ToDeclaration(LlmToolDefinition tool)
    {
        var declaration = new JsonObject { ["name"] = tool.Name, ["description"] = tool.Description };
        // A tool without arguments omits the schema.
        if (tool.ParametersSchema.ValueKind == JsonValueKind.Object && tool.ParametersSchema.EnumerateObject().Any())
        {
            declaration["parametersJsonSchema"] = Clone(tool.ParametersSchema);
        }

        return declaration;
    }

    private static JsonObject ToContent(LlmMessage message)
    {
        var parts = new JsonArray();
        switch (message.Role)
        {
            case LlmRole.User:
                parts.Add(new JsonObject { ["text"] = message.Text ?? string.Empty });
                break;

            case LlmRole.Model:
                if (!string.IsNullOrEmpty(message.Text))
                {
                    parts.Add(new JsonObject { ["text"] = message.Text });
                }

                foreach (var call in message.ToolCalls)
                {
                    var functionCall = new JsonObject { ["name"] = call.Name, ["args"] = Clone(call.Arguments) };
                    if (call.Id is not null)
                    {
                        functionCall["id"] = call.Id;
                    }

                    var part = new JsonObject { ["functionCall"] = functionCall };
                    if (call.ProviderState is not null)
                    {
                        part["thoughtSignature"] = call.ProviderState; // Gemini 3: must be echoed back.
                    }

                    parts.Add(part);
                }

                break;

            default:
                foreach (var result in message.ToolResults)
                {
                    var functionResponse = new JsonObject { ["name"] = result.Name, ["response"] = Clone(result.Response) };
                    if (result.CallId is not null)
                    {
                        functionResponse["id"] = result.CallId;
                    }

                    parts.Add(new JsonObject { ["functionResponse"] = functionResponse });
                }

                break;
        }

        return new JsonObject
        {
            ["role"] = message.Role == LlmRole.Model ? "model" : "user",
            ["parts"] = parts,
        };
    }

    private static JsonNode? Clone(JsonElement element) => JsonNode.Parse(element.GetRawText());
}
