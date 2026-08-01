using System.Text.Json.Nodes;

namespace McpAdapter.Infrastructure;

/// <summary>
/// A single MCP tool — name, metadata, and execution.
/// Each implementation is a thin wrapper over QueryServiceClient.
/// </summary>
public interface IMcpTool
{
    string Name { get; }
    string Description { get; }
    JsonObject InputSchema { get; }
    Task<string> ExecuteAsync(IDictionary<string, object?> arguments, CancellationToken ct);
}
