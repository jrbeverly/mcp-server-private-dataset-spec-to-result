using System.Text.Json;

namespace EvaluationHarness.Models;

public sealed record BenchmarkQuestion
{
    public required string Id { get; init; }
    public required string ToolFamily { get; init; }
    public required string Title { get; init; }
    public required string Prompt { get; init; }
    public required string ToolName { get; init; }
    public required Dictionary<string, object?> Arguments { get; init; }
    public required string ExpectedBehavior { get; init; }
}
