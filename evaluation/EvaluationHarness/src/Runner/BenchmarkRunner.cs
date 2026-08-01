using System.Diagnostics;
using System.Text.Json;
using EvaluationHarness.Models;
using EvaluationHarness.Storage;

namespace EvaluationHarness.Runner;

public sealed class BenchmarkRunner
{
    private readonly QueryServiceAdapter _adapter;
    private readonly IResultStore _store;

    public BenchmarkRunner(QueryServiceAdapter adapter, IResultStore store)
    {
        _adapter = adapter;
        _store = store;
    }

    public async Task<BenchmarkRunResult> RunAsync(
        string runLabel,
        IReadOnlyList<BenchmarkQuestion> questions,
        CancellationToken ct = default)
    {
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-") + Guid.NewGuid().ToString("N")[..8];
        var startedAt = DateTimeOffset.UtcNow.ToString("O");
        var sw = Stopwatch.StartNew();

        var results = new List<QuestionResult>(questions.Count);
        foreach (var question in questions)
        {
            var result = await _adapter.ExecuteQuestionAsync(question, ct);
            results.Add(result);
        }

        sw.Stop();

        var passed = results.Count(r => r.Status == "passed");
        var failed = results.Count(r => r.Status == "failed");

        var runResult = new BenchmarkRunResult
        {
            RunId = runId,
            RunLabel = runLabel,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            ServiceUrl = string.Empty,
            Results = results,
            Summary = new RunSummary
            {
                TotalQuestions = results.Count,
                Passed = passed,
                Failed = failed,
                AverageLatencyMs = results.Count > 0 ? results.Average(r => r.LatencyMs) : 0
            }
        };

        await _store.SaveAsync(runResult, ct);
        return runResult;
    }
}
