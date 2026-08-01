using System.Text.Json.Nodes;
using McpAdapter.Infrastructure;
using ServiceContract.V1;

namespace McpAdapter.Tools;

public sealed class AggregateTool : IMcpTool
{
    private readonly QueryServiceClient _client;

    public AggregateTool(QueryServiceClient client) => _client = client;

    public string Name => "aggregate";
    public string Description => "Return counts, rankings, distributions, and metric summaries across categories. Group by category to understand dataset composition. Use a numeric metric name to see statistical summaries with min, max, avg, and sum per category bucket. Supports filtering and sorting of result buckets.";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["metric"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Metric to aggregate: 'count' for entity counts per category, or a numeric metric name for statistical summaries (avg, min, max, sum)"
            },
            ["groupBy"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray { "category" },
                ["description"] = "Dimension to group by"
            },
            ["filter"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Filter result buckets whose key or label contains this text"
            },
            ["sort"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Sort order: count_desc (default), count_asc, value_desc, value_asc, key_asc, key_desc"
            },
            ["datasetVersion"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Pin to a specific dataset version (defaults to active version)"
            },
            ["limit"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = "Maximum buckets to return",
                ["minimum"] = 1
            }
        },
        ["required"] = new JsonArray { "metric" }
    };

    public async Task<string> ExecuteAsync(IDictionary<string, object?> arguments, CancellationToken ct)
    {
        var request = new AggregateRequest
        {
            Metric = (string)arguments["metric"]!,
            GroupBy = GetString(arguments, "groupBy"),
            Filter = GetString(arguments, "filter"),
            Sort = GetString(arguments, "sort"),
            DatasetVersion = GetString(arguments, "datasetVersion"),
            Limit = GetInt(arguments, "limit")
        };

        var response = await _client.AggregateAsync(request, ct);
        return McpToolResult.Format(response);
    }

    private static string? GetString(IDictionary<string, object?> args, string key)
        => args.TryGetValue(key, out var v) && v is string s ? s : null;

    private static int? GetInt(IDictionary<string, object?> args, string key)
        => args.TryGetValue(key, out var v) && v is not null
            ? Convert.ToInt32(v)
            : null;
}
