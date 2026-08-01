using EvaluationHarness.Models;
using EvaluationHarness.Storage;
using Xunit;

namespace EvaluationHarness.Tests.Storage;

public class FileSystemResultStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"eval-test-{Guid.NewGuid():N}");
        try
        {
            var store = new FileSystemResultStore(dir);
            var run = CreateRun("run-001", "test-run");

            await store.SaveAsync(run);
            var loaded = await store.LoadAsync("run-001");

            Assert.NotNull(loaded);
            Assert.Equal("run-001", loaded!.RunId);
            Assert.Equal("test-run", loaded.RunLabel);
            Assert.Equal(3, loaded.Results.Count);
            Assert.Equal(2, loaded.Summary!.Passed);
            Assert.Equal(1, loaded.Summary.Failed);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Load_ReturnsNull_WhenNotFound()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"eval-test-{Guid.NewGuid():N}");
        try
        {
            var store = new FileSystemResultStore(dir);
            var loaded = await store.LoadAsync("nonexistent");
            Assert.Null(loaded);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ListRunIds_ReturnsSavedRuns()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"eval-test-{Guid.NewGuid():N}");
        try
        {
            var store = new FileSystemResultStore(dir);
            await store.SaveAsync(CreateRun("run-a", "A"));
            await store.SaveAsync(CreateRun("run-b", "B"));

            var ids = await store.ListRunIdsAsync();
            Assert.Contains("run-a", ids);
            Assert.Contains("run-b", ids);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    private static BenchmarkRunResult CreateRun(string runId, string label) => new()
    {
        RunId = runId,
        RunLabel = label,
        StartedAt = DateTimeOffset.UtcNow.ToString("O"),
        CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
        ServiceUrl = "http://test/",
        Results = new List<QuestionResult>
        {
            new() { QuestionId = "Q1", ToolFamily = "discovery", ToolName = "discover", Status = "passed", LatencyMs = 50 },
            new() { QuestionId = "Q2", ToolFamily = "aggregate", ToolName = "aggregate", Status = "passed", LatencyMs = 30 },
            new() { QuestionId = "Q3", ToolFamily = "drill_down", ToolName = "drill_down", Status = "failed", ErrorMessage = "Not found", LatencyMs = 10 }
        },
        Summary = new RunSummary { TotalQuestions = 3, Passed = 2, Failed = 1, AverageLatencyMs = 30 }
    };
}
