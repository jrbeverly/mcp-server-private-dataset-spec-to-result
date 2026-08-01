namespace EvaluationHarness.Models;

public sealed record ScoredRun
{
    public required string RunId { get; init; }
    public required string ScoredAt { get; init; }
    public required string RubricId { get; init; }
    public required IReadOnlyList<ScoredQuestion> Scores { get; init; }
    public ScoredRunSummary? Summary { get; init; }
}

public sealed record ScoredQuestion
{
    public required string QuestionId { get; init; }
    public required string ToolFamily { get; init; }
    public required IReadOnlyList<DimensionScore> Dimensions { get; init; }
    public string? Notes { get; init; }
    public string? FailureCategory { get; init; }
}

public sealed record DimensionScore
{
    public required string DimensionName { get; init; }
    public required int Score { get; init; }
    public string? Comment { get; init; }
}

public sealed record ScoredRunSummary
{
    public required int TotalQuestions { get; init; }
    public required double OverallAverage { get; init; }
    public required IReadOnlyDictionary<string, double> AveragesByDimension { get; init; }
    public required IReadOnlyDictionary<string, double> AveragesByToolFamily { get; init; }
}

public static class FailureCategories
{
    public const string ToolSurface = "tool_surface";
    public const string CorpusQuality = "corpus_quality";
    public const string ResponseShaping = "response_shaping";
    public const string None = "none";
}
