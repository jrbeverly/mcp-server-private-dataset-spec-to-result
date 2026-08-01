using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using EvaluationHarness.Models;
using ServiceContract.V1;

namespace EvaluationHarness.Runner;

public sealed class QueryServiceAdapter
{
    private readonly HttpClient _http;
    private readonly string _serviceUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public QueryServiceAdapter(HttpClient http, string serviceUrl)
    {
        _http = http;
        _serviceUrl = serviceUrl.TrimEnd('/');
    }

    public async Task<QuestionResult> ExecuteQuestionAsync(
        BenchmarkQuestion question, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var response = question.ToolName switch
            {
                "discover" => await PostAsync<DiscoveryRequest, DiscoveryResponse>(
                    "/api/v1/discovery", ToDiscoveryRequest(question.Arguments), ct),
                "aggregate" => await PostAsync<AggregateRequest, AggregateResponse>(
                    "/api/v1/aggregation", ToAggregateRequest(question.Arguments), ct),
                "explore_metadata" => await PostAsync<MetadataExplorationRequest, MetadataExplorationResponse>(
                    "/api/v1/metadata", ToMetadataRequest(question.Arguments), ct),
                "drill_down" => await PostAsync<DrillDownRequest, DrillDownResponse>(
                    "/api/v1/drill-down", ToDrillDownRequest(question.Arguments), ct),
                "get_evidence" => await PostAsync<RawEvidenceRequest, RawEvidenceResponse>(
                    "/api/v1/raw-evidence", ToEvidenceRequest(question.Arguments), ct),
                _ => throw new InvalidOperationException($"Unknown tool: {question.ToolName}")
            };

            sw.Stop();
            var responseJson = JsonSerializer.SerializeToElement(response, JsonOptions);

            return new QuestionResult
            {
                QuestionId = question.Id,
                ToolFamily = question.ToolFamily,
                ToolName = question.ToolName,
                Status = "passed",
                Response = responseJson,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new QuestionResult
            {
                QuestionId = question.Id,
                ToolFamily = question.ToolFamily,
                ToolName = question.ToolName,
                Status = "failed",
                ErrorMessage = ex.Message,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
    }

    private async Task<JsonElement> PostAsync<TRequest, TResponse>(
        string path, TRequest body, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(path, body, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct));
    }

    private static DiscoveryRequest ToDiscoveryRequest(Dictionary<string, object?> args) => new()
    {
        Query = GetString(args, "query"),
        Category = GetString(args, "category"),
        Offset = GetInt(args, "offset"),
        Limit = GetInt(args, "limit"),
        DatasetVersion = GetString(args, "datasetVersion")
    };

    private static AggregateRequest ToAggregateRequest(Dictionary<string, object?> args) => new()
    {
        Metric = (string)args["metric"]!,
        GroupBy = GetString(args, "groupBy"),
        Filter = GetString(args, "filter"),
        Sort = GetString(args, "sort"),
        Limit = GetInt(args, "limit"),
        DatasetVersion = GetString(args, "datasetVersion")
    };

    private static MetadataExplorationRequest ToMetadataRequest(Dictionary<string, object?> args) => new()
    {
        Category = GetString(args, "category"),
        DatasetVersion = GetString(args, "datasetVersion")
    };

    private static DrillDownRequest ToDrillDownRequest(Dictionary<string, object?> args) => new()
    {
        EntityId = (string)args["entityId"]!,
        DatasetVersion = GetString(args, "datasetVersion")
    };

    private static RawEvidenceRequest ToEvidenceRequest(Dictionary<string, object?> args) => new()
    {
        EntityId = (string)args["entityId"]!,
        EvidenceType = GetString(args, "type"),
        Offset = GetInt(args, "offset"),
        Limit = GetInt(args, "limit"),
        DatasetVersion = GetString(args, "datasetVersion")
    };

    private static string? GetString(Dictionary<string, object?> args, string key)
        => args.TryGetValue(key, out var v) && v is string s ? s : null;

    private static int? GetInt(Dictionary<string, object?> args, string key)
        => args.TryGetValue(key, out var v) && v is not null
            ? Convert.ToInt32(v)
            : null;
}
