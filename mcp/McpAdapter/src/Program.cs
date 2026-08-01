using McpAdapter.Infrastructure;
using McpAdapter.Middleware;
using McpAdapter.Tools;
using ServiceConfiguration;

var builder = WebApplication.CreateBuilder(args);

// Configuration — environment-first
var config = ServiceBootstrap.Load();
builder.Services.AddSingleton(config);

// Typed HTTP client for calling the query service
// The MCP adapter calls the query service via HTTP — it does NOT
// reference QueryService.Domain or QueryService.Infrastructure directly.
builder.Services.AddHttpClient<QueryServiceClient>(client =>
{
    var baseUrl = Environment.GetEnvironmentVariable("QUERY_SERVICE_URL")
        ?? "http://localhost:5000";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add(
        config.Auth.HeaderName ?? "X-Api-Token",
        config.Auth.IssuedToken);
});

// MCP tool handlers — thin wrappers over QueryServiceClient
builder.Services.AddSingleton<IMcpTool, DiscoverTool>();
builder.Services.AddSingleton<IMcpTool, AggregateTool>();
builder.Services.AddSingleton<IMcpTool, ExploreMetadataTool>();
builder.Services.AddSingleton<IMcpTool, DrillDownTool>();
builder.Services.AddSingleton<IMcpTool, GetEvidenceTool>();

// MCP JSON-RPC handler
builder.Services.AddSingleton(sp =>
{
    var tools = sp.GetRequiredService<IEnumerable<IMcpTool>>();
    var serverName = config.Corpus.CorpusId;
    return new McpJsonRpcHandler(tools, serverName);
});

var app = builder.Build();

// Token enforcement for all routes except the public well-known discovery endpoint.
// Unauthorized requests fail closed and do not leak dataset contents or metadata.
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/.well-known"),
    builder => builder.UseMiddleware<StaticTokenMiddleware>());

// MCP Streamable HTTP endpoint
app.MapPost("/mcp", async (HttpRequest request, McpJsonRpcHandler handler, CancellationToken ct) =>
    await handler.HandleAsync(request, ct));

// Server metadata
app.MapGet("/.well-known/mcp", () => Results.Ok(new { status = "ok" }));

app.Run();
