using System.Text.Json.Nodes;
using McpAdapter.Infrastructure;
using ServiceContract.V1;

namespace McpAdapter.Tools;

public sealed class DiscoverTool : IMcpTool
{
    private readonly QueryServiceClient _client;

    public DiscoverTool(QueryServiceClient client) => _client = client;

    public string Name => "discover";
    public string Description => "Find entities, cohorts, and categories in the dataset. Supports free-text search across names and descriptions, category filtering, and pagination. Returns relevance-ranked results with entity identifiers ready for drill-down.";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Free-text search across entity names and descriptions"
            },
            ["category"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Filter results to a specific category"
            },
            ["datasetVersion"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Pin to a specific dataset version (defaults to active version)"
            },
            ["offset"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = "Pagination offset",
                ["minimum"] = 0
            },
            ["limit"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = "Maximum results to return (default 20, max 100)",
                ["minimum"] = 1,
                ["maximum"] = 100
            }
        }
    };

    public async Task<string> ExecuteAsync(IDictionary<string, object?> arguments, CancellationToken ct)
    {
        var request = new DiscoveryRequest
        {
            Query = GetString(arguments, "query"),
            Category = GetString(arguments, "category"),
            DatasetVersion = GetString(arguments, "datasetVersion"),
            Offset = GetInt(arguments, "offset"),
            Limit = GetInt(arguments, "limit")
        };

        var response = await _client.DiscoverAsync(request, ct);
        return McpToolResult.Format(response);
    }

    private static string? GetString(IDictionary<string, object?> args, string key)
        => args.TryGetValue(key, out var v) && v is string s ? s : null;

    private static int? GetInt(IDictionary<string, object?> args, string key)
        => args.TryGetValue(key, out var v) && v is not null
            ? Convert.ToInt32(v)
            : null;
}
