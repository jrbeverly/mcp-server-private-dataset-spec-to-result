using System.Text.Json;

namespace McpAdapter.Infrastructure;

public static class McpToolResult
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Format<T>(T result)
    {
        return JsonSerializer.Serialize(result, Options);
    }
}
