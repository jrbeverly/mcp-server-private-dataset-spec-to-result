using System.Text.Json;
using System.Text.Json.Nodes;

namespace McpAdapter.Infrastructure;

/// <summary>
/// Handles MCP JSON-RPC 2.0 messages over Streamable HTTP.
/// Routes initialize, tools/list, and tools/call to the correct handlers.
/// </summary>
public sealed class McpJsonRpcHandler
{
    private readonly IReadOnlyDictionary<string, IMcpTool> _tools;
    private readonly string _serverName;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public McpJsonRpcHandler(IEnumerable<IMcpTool> tools, string serverName)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        _serverName = serverName;
    }

    public async Task<IResult> HandleAsync(HttpRequest request, CancellationToken ct)
    {
        JsonObject body;
        try
        {
            body = (await request.ReadFromJsonAsync<JsonObject>(Options, ct))!;
        }
        catch (JsonException)
        {
            return Results.BadRequest(ErrorResponse(null, -32700, "Parse error"));
        }

        var method = (string?)body["method"];
        var id = body["id"];
        var idValue = id?.GetValueKind() is JsonValueKind.String or JsonValueKind.Number
            ? id.Deserialize<JsonElement>() : (JsonElement?)null;

        // Notifications (no id) — no response expected
        if (id is null)
            return Results.NoContent();

        try
        {
            var resultJson = method switch
            {
                "initialize" => HandleInitialize(),
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolsCallAsync(body, ct),
                _ => throw new JsonRpcException(-32601, $"Method not found: '{method}'")
            };

            return Results.Ok(new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = idValue,
                Result = resultJson
            });
        }
        catch (JsonRpcException ex)
        {
            return Results.Ok(new JsonRpcErrorResponse
            {
                JsonRpc = "2.0",
                Id = idValue,
                Error = new JsonRpcError { Code = ex.Code, Message = ex.Message }
            });
        }
        catch (Exception)
        {
            return Results.Ok(new JsonRpcErrorResponse
            {
                JsonRpc = "2.0",
                Id = idValue,
                Error = new JsonRpcError { Code = -32603, Message = "Internal error" }
            });
        }
    }

    private JsonObject HandleInitialize()
    {
        return new JsonObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = _serverName,
                ["version"] = "1.0.0"
            }
        };
    }

    private JsonObject HandleToolsList()
    {
        var toolsArray = new JsonArray();
        foreach (var tool in _tools.Values)
        {
            toolsArray.Add(new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.InputSchema
            });
        }
        return new JsonObject { ["tools"] = toolsArray };
    }

    private async Task<JsonObject> HandleToolsCallAsync(JsonObject body, CancellationToken ct)
    {
        var paramsNode = body["params"];
        if (paramsNode is not JsonObject parameters)
            throw new JsonRpcException(-32602, "Missing params object");

        var name = (string?)parameters["name"];
        if (string.IsNullOrWhiteSpace(name))
            throw new JsonRpcException(-32602, "Missing tool name");

        if (!_tools.TryGetValue(name, out var tool))
            throw new JsonRpcException(-32602, $"Unknown tool: '{name}'");

        var arguments = parameters["arguments"];
        var args = arguments is JsonObject argsObj
            ? argsObj.ToDictionary(kvp => kvp.Key, kvp => (object?)UnwrapNode(kvp.Value))
            : new Dictionary<string, object?>();

        var text = await tool.ExecuteAsync(args, ct);

        return new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = text
                }
            }
        };
    }

    private static object? UnwrapNode(JsonNode? node)
    {
        if (node is null) return null;

        if (node is JsonValue v)
        {
            var value = v.GetValue<object?>();
            // JsonValue.GetValue<object?>() returns JsonElement for numbers;
            // unwrap it to the appropriate .NET primitive so callers receive
            // int/long/double/string/bool instead of JsonElement.
            if (value is JsonElement je)
                value = UnwrapJsonElement(je);
            return value;
        }

        return node.ToJsonString();
    }

    private static object? UnwrapJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i)
                ? i
                : element.TryGetInt64(out var l)
                    ? l
                    : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element
        };
    }

    private static JsonObject ErrorResponse(JsonElement? id, int code, string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.HasValue ? JsonSerializer.SerializeToNode(id.Value) : null,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
    }

    private sealed class JsonRpcException : Exception
    {
        public int Code { get; }
        public JsonRpcException(int code, string message) : base(message) => Code = code;
    }

    // Types used for structured serialization
    public sealed record JsonRpcResponse
    {
        public required string JsonRpc { get; init; }
        public JsonElement? Id { get; init; }
        public required JsonObject Result { get; init; }
    }

    public sealed record JsonRpcErrorResponse
    {
        public required string JsonRpc { get; init; }
        public JsonElement? Id { get; init; }
        public required JsonRpcError Error { get; init; }
    }

    public sealed record JsonRpcError
    {
        public required int Code { get; init; }
        public required string Message { get; init; }
    }
}
