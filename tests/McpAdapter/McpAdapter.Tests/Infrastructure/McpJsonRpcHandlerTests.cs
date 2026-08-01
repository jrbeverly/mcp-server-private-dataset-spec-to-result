using System.Text.Json;
using System.Text.Json.Nodes;
using McpAdapter.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpAdapter.Tests.Infrastructure;

public class McpJsonRpcHandlerTests
{
    private readonly McpJsonRpcHandler _handler;

    public McpJsonRpcHandlerTests()
    {
        var tool = new FakeTool("test_tool", "A test tool");
        _handler = new McpJsonRpcHandler([tool], "test-server");
    }

    private static HttpRequest CreateRequest(JsonObject body)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, body);
        stream.Position = 0;

        var context = new DefaultHttpContext();
        context.Request.Body = stream;
        context.Request.ContentType = "application/json";
        return context.Request;
    }

    private static async Task<JsonObject?> ReadResponseBody(IResult result)
    {
        var httpContext = CreateHttpContext();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Position = 0;

        if (httpContext.Response.Body.Length == 0)
            return null;

        return await JsonSerializer.DeserializeAsync<JsonObject>(
            httpContext.Response.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    [Fact]
    public async Task Initialize_Returns_ServerInfo()
    {
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "initialize"
        };

        var result = await _handler.HandleAsync(CreateRequest(body), CancellationToken.None);
        var json = await ReadResponseBody(result);

        Assert.NotNull(json);
        Assert.Equal("2.0", (string?)json!["jsonrpc"]);
        Assert.NotNull(json["result"]?["serverInfo"]);
        Assert.Equal("test-server", (string?)json["result"]?["serverInfo"]?["name"]);
        Assert.NotNull(json["result"]?["capabilities"]);
    }

    [Fact]
    public async Task ToolsList_Returns_Registered_Tools()
    {
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 2,
            ["method"] = "tools/list"
        };

        var result = await _handler.HandleAsync(CreateRequest(body), CancellationToken.None);
        var json = await ReadResponseBody(result);

        Assert.NotNull(json);
        var tools = json!["result"]?["tools"]?.AsArray();
        Assert.NotNull(tools);
        Assert.Single(tools!);
        Assert.Equal("test_tool", (string?)tools![0]?["name"]);
    }

    [Fact]
    public async Task ToolsCall_Executes_Tool()
    {
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 3,
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = "test_tool",
                ["arguments"] = new JsonObject { ["key"] = "value" }
            }
        };

        var result = await _handler.HandleAsync(CreateRequest(body), CancellationToken.None);
        var json = await ReadResponseBody(result);

        Assert.NotNull(json);
        var content = json!["result"]?["content"]?.AsArray();
        Assert.NotNull(content);
        Assert.Single(content!);
        Assert.Equal("text", (string?)content![0]?["type"]);
        Assert.Equal("executed:test_tool", (string?)content[0]?["text"]);
    }

    [Fact]
    public async Task Unknown_Method_Returns_Error()
    {
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 4,
            ["method"] = "unknown/method"
        };

        var result = await _handler.HandleAsync(CreateRequest(body), CancellationToken.None);
        var json = await ReadResponseBody(result);

        Assert.NotNull(json);
        Assert.Equal(-32601, (int?)json!["error"]?["code"]);
        Assert.Equal(4, (int?)json["id"]);
    }

    [Fact]
    public async Task Notification_Returns_NoContent()
    {
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "notifications/initialized"
        };

        var result = await _handler.HandleAsync(CreateRequest(body), CancellationToken.None);
        var httpContext = CreateHttpContext();
        await result.ExecuteAsync(httpContext);

        Assert.Equal(204, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Parse_Error_Returns_ParseError()
    {
        var stream = new MemoryStream("{invalid json"u8.ToArray());
        var context = CreateHttpContext();
        context.Request.Body = stream;
        context.Request.ContentType = "application/json";

        var result = await _handler.HandleAsync(context.Request, CancellationToken.None);
        var httpContext = CreateHttpContext();
        await result.ExecuteAsync(httpContext);

        Assert.Equal(400, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Unknown_Tool_Returns_Error()
    {
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 5,
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = "nonexistent",
                ["arguments"] = new JsonObject()
            }
        };

        var result = await _handler.HandleAsync(CreateRequest(body), CancellationToken.None);
        var json = await ReadResponseBody(result);

        Assert.NotNull(json);
        Assert.Equal(-32602, (int?)json!["error"]?["code"]);
    }

    [Fact]
    public async Task Initialize_Returns_ProtocolVersion()
    {
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "initialize"
        };

        var result = await _handler.HandleAsync(CreateRequest(body), CancellationToken.None);
        var json = await ReadResponseBody(result);

        Assert.Equal("2024-11-05", (string?)json!["result"]?["protocolVersion"]);
    }

    private sealed class FakeTool : IMcpTool
    {
        public string Name { get; }
        public string Description { get; }
        public JsonObject InputSchema => new() { ["type"] = "object" };

        public FakeTool(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public Task<string> ExecuteAsync(IDictionary<string, object?> arguments, CancellationToken ct)
            => Task.FromResult($"executed:{Name}");
    }
}
