using System.Text.Json.Nodes;
using McpAdapter.Infrastructure;
using ServiceContract.V1;

namespace McpAdapter.Tools;

public sealed class GetEvidenceTool : IMcpTool
{
    private readonly QueryServiceClient _client;

    public GetEvidenceTool(QueryServiceClient client) => _client = client;

    public string Name => "get_evidence";
    public string Description => "Retrieve raw supporting records for a specific entity. Returns the underlying source evidence used to compile the analytical views. Use this to verify claims, inspect source data, or provide supporting detail. Evidence is paginated and filterable by type.";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["entityId"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Entity identifier to retrieve evidence for"
            },
            ["evidenceType"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Filter evidence by type (e.g., 'pricing', 'listing')"
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
                ["description"] = "Maximum records to return (default 20, max 100)",
                ["minimum"] = 1,
                ["maximum"] = 100
            }
        },
        ["required"] = new JsonArray { "entityId" }
    };

    public async Task<string> ExecuteAsync(IDictionary<string, object?> arguments, CancellationToken ct)
    {
        var request = new RawEvidenceRequest
        {
            EntityId = (string)arguments["entityId"]!,
            EvidenceType = GetString(arguments, "evidenceType"),
            DatasetVersion = GetString(arguments, "datasetVersion"),
            Offset = GetInt(arguments, "offset"),
            Limit = GetInt(arguments, "limit")
        };

        var response = await _client.GetEvidenceAsync(request, ct);
        return McpToolResult.Format(response);
    }

    private static string? GetString(IDictionary<string, object?> args, string key)
        => args.TryGetValue(key, out var v) && v is string s ? s : null;

    private static int? GetInt(IDictionary<string, object?> args, string key)
        => args.TryGetValue(key, out var v) && v is not null
            ? Convert.ToInt32(v)
            : null;
}
