using System.Text.Json.Nodes;
using McpAdapter.Infrastructure;
using ServiceContract.V1;

namespace McpAdapter.Tools;

public sealed class ExploreMetadataTool : IMcpTool
{
    private readonly QueryServiceClient _client;

    public ExploreMetadataTool(QueryServiceClient client) => _client = client;

    public string Name => "explore_metadata";
    public string Description => "Explore dataset dimensions, categories, and available metrics. Returns category distributions, total entity/category counts, and lists of available metric and property keys. Use this before discovery or aggregation to understand what dimensions exist in the corpus.";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["category"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Filter dimension exploration to a specific category"
            },
            ["datasetVersion"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Pin to a specific dataset version (defaults to active version)"
            }
        }
    };

    public async Task<string> ExecuteAsync(IDictionary<string, object?> arguments, CancellationToken ct)
    {
        var request = new MetadataExplorationRequest
        {
            Category = GetString(arguments, "category"),
            DatasetVersion = GetString(arguments, "datasetVersion")
        };

        var response = await _client.ExploreMetadataAsync(request, ct);
        return McpToolResult.Format(response);
    }

    private static string? GetString(IDictionary<string, object?> args, string key)
        => args.TryGetValue(key, out var v) && v is string s ? s : null;
}
