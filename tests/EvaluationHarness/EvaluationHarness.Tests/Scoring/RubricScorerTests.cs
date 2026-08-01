using System.Text.Json;
using EvaluationHarness.Models;
using EvaluationHarness.Scoring;
using EvaluationHarness.Storage;
using Xunit;

namespace EvaluationHarness.Tests.Scoring;

public class RubricScorerTests
{
    [Fact]
    public async Task AutoScore_PassedQuestion_GetsPositiveScores()
    {
        var store = new TestResultStore();
        var run = CreateRunWithResults(
            new QuestionResult { QuestionId = "Q1", ToolFamily = "discovery", ToolName = "discover", Status = "passed", Response = CreateResponse("entities", true), LatencyMs = 50 });
        await store.SaveAsync(run);

        var scorer = new RubricScorer(store);
        var scored = await scorer.AutoScoreAsync(run.RunId, ScoringRubric.Default);

        var qScore = scored.Scores.Single();
        var avg = qScore.Dimensions.Average(d => (double)d.Score);
        Assert.True(avg > 2.5, $"Expected avg > 2.5 but got {avg:F2}");
        Assert.Equal(FailureCategories.None, qScore.FailureCategory);
    }

    [Fact]
    public async Task AutoScore_FailedQuestion_FlagsToolSurface()
    {
        var store = new TestResultStore();
        var run = CreateRunWithResults(
            new QuestionResult { QuestionId = "Q1", ToolFamily = "discovery", ToolName = "discover", Status = "failed", ErrorMessage = "timeout", LatencyMs = 5000 });
        await store.SaveAsync(run);

        var scorer = new RubricScorer(store);
        var scored = await scorer.AutoScoreAsync(run.RunId, ScoringRubric.Default);

        var qScore = scored.Scores.Single();
        Assert.Equal(FailureCategories.ToolSurface, qScore.FailureCategory);
        Assert.True(qScore.Dimensions.All(d => d.Score <= 2));
    }

    [Fact]
    public async Task AutoScore_EmptyResponse_FlagsResponseShaping()
    {
        var store = new TestResultStore();
        var run = CreateRunWithResults(
            new QuestionResult { QuestionId = "Q1", ToolFamily = "discovery", ToolName = "discover", Status = "passed", Response = null, LatencyMs = 10 });
        await store.SaveAsync(run);

        var scorer = new RubricScorer(store);
        var scored = await scorer.AutoScoreAsync(run.RunId, ScoringRubric.Default);

        var qScore = scored.Scores.Single();
        Assert.Equal(FailureCategories.ResponseShaping, qScore.FailureCategory);
    }

    [Fact]
    public async Task AutoScore_ProducesSummaryAverages()
    {
        var store = new TestResultStore();
        var run = CreateRunWithResults(
            new QuestionResult { QuestionId = "Q1", ToolFamily = "discovery", ToolName = "discover", Status = "passed", Response = CreateResponse("entities", true), LatencyMs = 50 },
            new QuestionResult { QuestionId = "Q2", ToolFamily = "aggregate", ToolName = "aggregate", Status = "passed", Response = CreateResponse("buckets", true), LatencyMs = 30 });
        await store.SaveAsync(run);

        var scorer = new RubricScorer(store);
        var scored = await scorer.AutoScoreAsync(run.RunId, ScoringRubric.Default);

        Assert.NotNull(scored.Summary);
        Assert.Equal(2, scored.Summary!.TotalQuestions);
        Assert.True(scored.Summary.OverallAverage > 2.5);

        Assert.NotNull(scored.Summary.AveragesByDimension);
        Assert.Contains("Relevance", scored.Summary.AveragesByDimension.Keys);
        Assert.Contains("Completeness", scored.Summary.AveragesByDimension.Keys);

        Assert.NotNull(scored.Summary.AveragesByToolFamily);
        Assert.Contains("discovery", scored.Summary.AveragesByToolFamily.Keys);
        Assert.Contains("aggregate", scored.Summary.AveragesByToolFamily.Keys);
    }

    [Fact]
    public void Compare_DetectsRegression()
    {
        var store = new TestResultStore();
        var scorer = new RubricScorer(store);

        var left = new ScoredRun
        {
            RunId = "left",
            ScoredAt = "2026-01-01T00:00:00Z",
            RubricId = ScoringRubric.Default.Id,
            Scores = new List<ScoredQuestion>
            {
                new()
                {
                    QuestionId = "Q1", ToolFamily = "discovery",
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionName = "Relevance", Score = 5 },
                        new() { DimensionName = "Completeness", Score = 4 }
                    }
                }
            }
        };

        var right = new ScoredRun
        {
            RunId = "right",
            ScoredAt = "2026-02-01T00:00:00Z",
            RubricId = ScoringRubric.Default.Id,
            Scores = new List<ScoredQuestion>
            {
                new()
                {
                    QuestionId = "Q1", ToolFamily = "discovery",
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionName = "Relevance", Score = 2 },
                        new() { DimensionName = "Completeness", Score = 3 }
                    }
                }
            }
        };

        var report = scorer.Compare(left, "v1-corpus", right, "v2-corpus");

        Assert.Single(report.QuestionComparisons);
        Assert.True(report.QuestionComparisons[0].Regressed);
        Assert.Equal(1, report.Summary.Regressed);
        Assert.True(report.Summary.OverallDelta < 0);
    }

    [Fact]
    public void Compare_DetectsImprovement()
    {
        var store = new TestResultStore();
        var scorer = new RubricScorer(store);

        var left = new ScoredRun
        {
            RunId = "left",
            ScoredAt = "2026-01-01T00:00:00Z",
            RubricId = ScoringRubric.Default.Id,
            Scores = new List<ScoredQuestion>
            {
                new()
                {
                    QuestionId = "Q1", ToolFamily = "drill_down",
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionName = "Relevance", Score = 2 }
                    }
                }
            }
        };

        var right = new ScoredRun
        {
            RunId = "right",
            ScoredAt = "2026-02-01T00:00:00Z",
            RubricId = ScoringRubric.Default.Id,
            Scores = new List<ScoredQuestion>
            {
                new()
                {
                    QuestionId = "Q1", ToolFamily = "drill_down",
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionName = "Relevance", Score = 5 }
                    }
                }
            }
        };

        var report = scorer.Compare(left, "v1", right, "v2");

        Assert.True(report.QuestionComparisons[0].Improved);
        Assert.Equal(1, report.Summary.Improved);
    }

    private static BenchmarkRunResult CreateRunWithResults(params QuestionResult[] results) => new()
    {
        RunId = "test-run",
        RunLabel = "test",
        StartedAt = DateTimeOffset.UtcNow.ToString("O"),
        CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
        ServiceUrl = "http://test/",
        Results = results,
        Summary = new RunSummary
        {
            TotalQuestions = results.Length,
            Passed = results.Count(r => r.Status == "passed"),
            Failed = results.Count(r => r.Status == "failed"),
            AverageLatencyMs = results.Length > 0 ? results.Average(r => r.LatencyMs) : 0
        }
    };

    private static JsonElement CreateResponse(string field, bool withData)
    {
        var json = withData
            ? $$"""{"{{field}}":[{"id":"x"}],"datasetVersion":"v1"}"""
            : """{"datasetVersion":"v1"}""";
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private sealed class TestResultStore : IResultStore
    {
        private readonly Dictionary<string, BenchmarkRunResult> _store = new();

        public Task SaveAsync(BenchmarkRunResult result, CancellationToken ct = default)
        {
            _store[result.RunId] = result;
            return Task.CompletedTask;
        }

        public Task<BenchmarkRunResult?> LoadAsync(string runId, CancellationToken ct = default)
        {
            _store.TryGetValue(runId, out var result);
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<string>> ListRunIdsAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(_store.Keys.ToList());
        }
    }
}
