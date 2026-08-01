using System.Net.Http.Json;
using System.Text.Json;
using ServiceContract.V1;

namespace McpAdapter.Infrastructure;

/// <summary>
/// Typed HTTP client that calls the query service API.
/// No analytical logic — just HTTP transport.
/// </summary>
public sealed class QueryServiceClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public QueryServiceClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<DiscoveryResponse> DiscoverAsync(
        DiscoveryRequest request, CancellationToken ct = default)
    {
        var response = await PostAsync("api/v1/discovery", request, ct);
        return (await response.Content.ReadFromJsonAsync<DiscoveryResponse>(JsonOptions, ct))!;
    }

    public async Task<AggregateResponse> AggregateAsync(
        AggregateRequest request, CancellationToken ct = default)
    {
        var response = await PostAsync("api/v1/aggregation", request, ct);
        return (await response.Content.ReadFromJsonAsync<AggregateResponse>(JsonOptions, ct))!;
    }

    public async Task<MetadataExplorationResponse> ExploreMetadataAsync(
        MetadataExplorationRequest request, CancellationToken ct = default)
    {
        var response = await PostAsync("api/v1/metadata", request, ct);
        return (await response.Content.ReadFromJsonAsync<MetadataExplorationResponse>(JsonOptions, ct))!;
    }

    public async Task<DrillDownResponse> DrillDownAsync(
        DrillDownRequest request, CancellationToken ct = default)
    {
        var response = await PostAsync("api/v1/drill-down", request, ct);
        return (await response.Content.ReadFromJsonAsync<DrillDownResponse>(JsonOptions, ct))!;
    }

    public async Task<RawEvidenceResponse> GetEvidenceAsync(
        RawEvidenceRequest request, CancellationToken ct = default)
    {
        var response = await PostAsync("api/v1/raw-evidence", request, ct);
        return (await response.Content.ReadFromJsonAsync<RawEvidenceResponse>(JsonOptions, ct))!;
    }

    private async Task<HttpResponseMessage> PostAsync<T>(
        string path, T body, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(path, body, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return response;
    }
}
