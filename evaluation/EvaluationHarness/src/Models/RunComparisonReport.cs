namespace EvaluationHarness.Models;

public sealed record RunComparisonReport
{
    public required string LeftRunId { get; init; }
    public required string LeftRunLabel { get; init; }
    public required string RightRunId { get; init; }
    public required string RightRunLabel { get; init; }
    public required string ComparedAt { get; init; }
    public required IReadOnlyList<QuestionComparison> QuestionComparisons { get; init; }
    public required ComparisonSummary Summary { get; init; }
}

public sealed record QuestionComparison
{
    public required string QuestionId { get; init; }
    public required string ToolFamily { get; init; }
    public double? LeftAverageScore { get; init; }
    public double? RightAverageScore { get; init; }
    public double? ScoreDelta { get; init; }
    public bool? Regressed { get; init; }
    public bool? Improved { get; init; }
    public string? Note { get; init; }
}

public sealed record ComparisonSummary
{
    public required int TotalQuestions { get; init; }
    public required int Improved { get; init; }
    public required int Regressed { get; init; }
    public required int Unchanged { get; init; }
    public required int Unscored { get; init; }
    public double? OverallLeftAverage { get; init; }
    public double? OverallRightAverage { get; init; }
    public double? OverallDelta { get; init; }
}
