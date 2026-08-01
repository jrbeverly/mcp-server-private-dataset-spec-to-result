using System.Text.Json.Nodes;
using McpAdapter.Infrastructure;
using ServiceContract.V1;

namespace McpAdapter.Tools;

public sealed class DrillDownTool : IMcpTool
{
    private readonly QueryServiceClient _client;

    public DrillDownTool(QueryServiceClient client) => _client = client;

    public string Name => "drill_down";
    public string Description => "Inspect a specific entity in detail. Returns the entity's full properties, metrics, description, and related entities in the same category. Use this after discovery identifies a promising target to get richer context before presenting findings.";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["entityId"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Entity identifier (from discovery results)"
            },
            ["datasetVersion"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Pin to a specific dataset version (defaults to active version)"
            }
        },
        ["required"] = new JsonArray { "entityId" }
    };

    public async Task<string> ExecuteAsync(IDictionary<string, object?> arguments, CancellationToken ct)
    {
        var request = new DrillDownRequest
        {
            EntityId = (string)arguments["entityId"]!,
            DatasetVersion = GetString(arguments, "datasetVersion")
        };

        var response = await _client.DrillDownAsync(request, ct);
        return McpToolResult.Format(response);
    }

    private static string? GetString(IDictionary<string, object?> args, string key)
        => args.TryGetValue(key, out var v) && v is string s ? s : null;
}
