using System.Text.Json;

namespace EvaluationHarness.Models;

public sealed record BenchmarkRunResult
{
    public required string RunId { get; init; }
    public required string RunLabel { get; init; }
    public required string StartedAt { get; init; }
    public required string CompletedAt { get; init; }
    public required string ServiceUrl { get; init; }
    public string? DatasetVersion { get; init; }
    public string? ServiceVersion { get; init; }
    public required IReadOnlyList<QuestionResult> Results { get; init; }
    public RunSummary? Summary { get; init; }
}

public sealed record QuestionResult
{
    public required string QuestionId { get; init; }
    public required string ToolFamily { get; init; }
    public required string ToolName { get; init; }
    public required string Status { get; init; }
    public JsonElement? Response { get; init; }
    public string? ErrorMessage { get; init; }
    public required long LatencyMs { get; init; }
}

public sealed record RunSummary
{
    public required int TotalQuestions { get; init; }
    public required int Passed { get; init; }
    public required int Failed { get; init; }
    public required double AverageLatencyMs { get; init; }
}
