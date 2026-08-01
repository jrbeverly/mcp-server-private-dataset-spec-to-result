using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using McpAdapter.Infrastructure;
using McpAdapter.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpAdapter.Tests.Tools;

public class McpToolE2ETests
{
    private static McpJsonRpcHandler CreateHandler()
    {
        var mockBackend = new MockQueryServiceHandler();
        var httpClient = new HttpClient(mockBackend) { BaseAddress = new Uri("http://test/") };
        var client = new QueryServiceClient(httpClient);

        var tools = new List<IMcpTool>
        {
            new DiscoverTool(client),
            new AggregateTool(client),
            new ExploreMetadataTool(client),
            new DrillDownTool(client),
            new GetEvidenceTool(client)
        };

        return new McpJsonRpcHandler(tools, "e2e-test-corpus");
    }

    // -- Discovery -------------------------------------------------------

    [Fact]
    public async Task ToolsCall_Discover_ReturnsRankedResults()
    {
        var handler = CreateHandler();
        var body = BuildToolsCall("discover", new JsonObject
        {
            ["query"] = "cloud",
            ["limit"] = 5
        });

        var json = await ExecuteAsync(handler, body);

        Assert.NotNull(json);
        Assert.Null(json!["error"]);
        Assert.Equal("2.0", (string?)json["jsonrpc"]);

        var inner = ParseToolResult(json);
        Assert.Equal("v1", inner.GetProperty("datasetVersion").GetString());
        Assert.Equal(2, inner.GetProperty("totalMatches").GetInt32());

        var entities = inner.GetProperty("entities").EnumerateArray().ToList();
        Assert.Equal(2, entities.Count);
        Assert.Equal("ent-001", entities[0].GetProperty("entityId").GetString());
        Assert.Equal("CloudCo Inc.", entities[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ToolsCall_Discover_AppliesPagination()
    {
        var handler = CreateHandler();
        var body = BuildToolsCall("discover", new JsonObject
        {
            ["query"] = "cloud",
            ["offset"] = 10,
            ["limit"] = 5
        });

        var json = await ExecuteAsync(handler, body);

        Assert.NotNull(json);
        Assert.Null(json!["error"]);
        var inner = ParseToolResult(json);
        _ = inner.GetProperty("datasetVersion");
    }

    // -- Aggregate -------------------------------------------------------

    [Fact]
    public async Task ToolsCall_Aggregate_ReturnsCategoryDistribution()
    {
        var handler = CreateHandler();
        var body = BuildToolsCall("aggregate", new JsonObject
        {
            ["metric"] = "count",
            ["groupBy"] = "category"
        });

        var json = await ExecuteAsync(handler, body);

        Assert.NotNull(json);
        Assert.Null(json!["error"]);
        var inner = ParseToolResult(json);
        Assert.Equal("count", inner.GetProperty("metric").GetString());

        var buckets = inner.GetProperty("buckets").EnumerateArray().ToList();
        Assert.Equal(2, buckets.Count);
        Assert.Equal("cat-saas", buckets[0].GetProperty("key").GetString());
    }

    [Fact]
    public async Task ToolsCall_Aggregate_HandlesSortAndLimit()
    {
        var handler = CreateHandler();
        var body = BuildToolsCall("aggregate", new JsonObject
        {
            ["metric"] = "count",
            ["groupBy"] = "category",
            ["sort"] = "count_desc",
            ["limit"] = 3
        });

        var json = await ExecuteAsync(handler, body);

        Assert.NotNull(json);
        Assert.Null(json!["error"]);
        var inner = ParseToolResult(json);
        var buckets = inner.GetProperty("buckets").EnumerateArray().ToList();
        Assert.NotEmpty(buckets);
    }

    // -- Drill-down ------------------------------------------------------

    [Fact]
    public async Task ToolsCall_DrillDown_ReturnsEntityDetail()
    {
        var handler = CreateHandler();
        var body = BuildToolsCall("drill_down", new JsonObject
        {
            ["entityId"] = "ent-001"
        });

        var json = await ExecuteAsync(handler, body);

        Assert.NotNull(json);
        Assert.Null(json!["error"]);
        var inner = ParseToolResult(json);
        Assert.Equal("v1", inner.GetProperty("datasetVersion").GetString());

        var entity = inner.GetProperty("entity");
        Assert.Equal("ent-001", entity.GetProperty("entityId").GetString());
        Assert.Equal("CloudCo Inc.", entity.GetProperty("name").GetString());

        var properties = entity.GetProperty("properties").EnumerateObject().ToList();
        Assert.Contains(properties, p => p.Name == "revenue" && p.Value.GetString() == "$50M");

        var related = inner.GetProperty("relatedEntities").EnumerateArray().ToList();
        Assert.Equal(2, related.Count);
        Assert.Equal("competitor", related[0].GetProperty("relationshipType").GetString());
    }

    // -- Initialize and tools/list ---------------------------------------

    [Fact]
    public async Task ToolsCall_Initialize_ReturnsServerInfo()
    {
        var handler = CreateHandler();
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "initialize"
        };

        var json = await ExecuteAsync(handler, body);

        Assert.NotNull(json);
        Assert.Null(json!["error"]);
        Assert.Equal("e2e-test-corpus", (string?)json["result"]?["serverInfo"]?["name"]);
        Assert.Equal("2024-11-05", (string?)json["result"]?["protocolVersion"]);
    }

    [Fact]
    public async Task ToolsList_ExposesAllFiveToolFamilies()
    {
        var handler = CreateHandler();
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/list"
        };

        var json = await ExecuteAsync(handler, body);

        Assert.NotNull(json);
        var tools = json!["result"]?["tools"]?.AsArray();
        Assert.NotNull(tools);
        Assert.Equal(5, tools!.Count);

        var names = tools.Select(t => (string?)t!["name"]).ToHashSet();
        Assert.Contains("discover", names);
        Assert.Contains("aggregate", names);
        Assert.Contains("explore_metadata", names);
        Assert.Contains("drill_down", names);
        Assert.Contains("get_evidence", names);
    }

    // -- Helpers ----------------------------------------------------------

    private static JsonObject BuildToolsCall(string name, JsonObject arguments)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = name,
                ["arguments"] = arguments
            }
        };
    }

    private static async Task<JsonObject?> ExecuteAsync(McpJsonRpcHandler handler, JsonObject body)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, body);
        stream.Position = 0;

        var requestCtx = new DefaultHttpContext();
        requestCtx.Request.Body = stream;
        requestCtx.Request.ContentType = "application/json";

        var result = await handler.HandleAsync(requestCtx.Request, CancellationToken.None);

        var responseCtx = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        responseCtx.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        await result.ExecuteAsync(responseCtx);
        responseCtx.Response.Body.Position = 0;

        if (responseCtx.Response.Body.Length == 0)
            return null;

        return await JsonSerializer.DeserializeAsync<JsonObject>(
            responseCtx.Response.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static JsonElement ParseToolResult(JsonObject? json)
    {
        var content = json!["result"]?["content"]?.AsArray();
        Assert.NotNull(content);
        Assert.Single(content!);
        var text = (string)content![0]!["text"]!;
        return JsonSerializer.Deserialize<JsonElement>(text);
    }

    private sealed class MockQueryServiceHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.TrimEnd('/');
            var json = path switch
            {
                "/api/v1/discovery" => DiscoveryResponse(),
                "/api/v1/aggregation" => AggregateResponse(),
                "/api/v1/metadata" => MetadataResponse(),
                "/api/v1/drill-down" => DrillDownResponse(),
                "/api/v1/raw-evidence" => EvidenceResponse(),
                _ => """{"error":"unknown_path"}"""
            };

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        private static string DiscoveryResponse() =>
            """{"datasetVersion":"v1","totalMatches":2,"entities":[{"entityId":"ent-001","name":"CloudCo Inc.","category":"cat-saas"},{"entityId":"ent-002","name":"Infra Ltd.","category":"cat-iaas"}]}""";

        private static string AggregateResponse() =>
            """{"datasetVersion":"v1","metric":"count","buckets":[{"key":"cat-saas","label":"SaaS","value":42,"count":42},{"key":"cat-iaas","label":"IaaS","value":18,"count":18}]}""";

        private static string MetadataResponse() =>
            """{"datasetVersion":"v1","totalEntityCount":100,"totalCategoryCount":5,"dimensions":[{"name":"Category","values":[{"value":"cat-saas","label":"SaaS","entityCount":42},{"value":"cat-iaas","label":"IaaS","entityCount":18}]}]}""";

        private static string DrillDownResponse() =>
            """{"datasetVersion":"v1","entity":{"entityId":"ent-001","name":"CloudCo Inc.","category":"cat-saas","description":"A cloud infrastructure provider.","properties":{"revenue":"$50M","headcount":"200"},"metrics":{"growth_rate":0.15,"market_share":0.08}},"relatedEntities":[{"entityId":"ent-002","name":"Infra Ltd.","relationshipType":"competitor","category":"cat-iaas"},{"entityId":"ent-003","name":"DataFlow Corp.","relationshipType":"partner","category":"cat-saas"}]}""";

        private static string EvidenceResponse() =>
            """{"datasetVersion":"v1","totalRecords":1,"records":[{"recordId":"rec-001","entityId":"ent-001","type":"pricing","fields":{"plan":"Enterprise","price":"$999/mo"}}]}""";
    }
}
