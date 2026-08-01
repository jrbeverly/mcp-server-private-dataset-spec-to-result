using System.Net;
using System.Text;
using System.Text.Json;
using EvaluationHarness.Models;
using EvaluationHarness.Runner;
using EvaluationHarness.Storage;
using Xunit;

namespace EvaluationHarness.Tests.Runner;

public class BenchmarkRunnerTests
{
    [Fact]
    public async Task Run_ExecutesAllQuestions()
    {
        var handler = new MockHttpHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test/") };
        var adapter = new QueryServiceAdapter(httpClient, "http://test/");
        var store = new TestResultStore();
        var runner = new BenchmarkRunner(adapter, store);

        var questions = new List<BenchmarkQuestion>
        {
            CreateQuestion("Q1", "discover", "discovery"),
            CreateQuestion("Q2", "aggregate", "aggregate")
        };

        var result = await runner.RunAsync("test-run", questions);

        Assert.Equal(2, result.Results.Count);
        Assert.Equal(2, result.Summary!.TotalQuestions);
        Assert.Equal(2, result.Summary.Passed);
        Assert.Equal(0, result.Summary.Failed);

        // Verify stored
        var stored = await store.LoadAsync(result.RunId);
        Assert.NotNull(stored);
        Assert.Equal(result.RunId, stored!.RunId);
    }

    [Fact]
    public async Task Run_RecordsFailures()
    {
        var handler = new FailingHttpHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test/") };
        var adapter = new QueryServiceAdapter(httpClient, "http://test/");
        var store = new TestResultStore();
        var runner = new BenchmarkRunner(adapter, store);

        var questions = new List<BenchmarkQuestion>
        {
            CreateQuestion("Q1", "discover", "discovery")
        };

        var result = await runner.RunAsync("failing-run", questions);

        Assert.Single(result.Results);
        Assert.Equal("failed", result.Results[0].Status);
        Assert.NotNull(result.Results[0].ErrorMessage);
        Assert.Equal(1, result.Summary!.Failed);
    }

    [Fact]
    public async Task Run_GeneratesUniqueRunId()
    {
        var handler = new MockHttpHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test/") };
        var adapter = new QueryServiceAdapter(httpClient, "http://test/");
        var store = new TestResultStore();
        var runner = new BenchmarkRunner(adapter, store);

        var questions = new List<BenchmarkQuestion> { CreateQuestion("Q1", "discover", "discovery") };

        var r1 = await runner.RunAsync("run-1", questions);
        var r2 = await runner.RunAsync("run-2", questions);

        Assert.NotEqual(r1.RunId, r2.RunId);
    }

    private static BenchmarkQuestion CreateQuestion(string id, string toolName, string toolFamily) => new()
    {
        Id = id,
        ToolFamily = toolFamily,
        Title = "Test question",
        Prompt = "Test prompt",
        ToolName = toolName,
        Arguments = toolName switch
        {
            "discover" => new Dictionary<string, object?> { ["query"] = "test" },
            "aggregate" => new Dictionary<string, object?> { ["metric"] = "count" },
            "explore_metadata" => new Dictionary<string, object?>(),
            "drill_down" => new Dictionary<string, object?> { ["entityId"] = "ent-001" },
            "get_evidence" => new Dictionary<string, object?> { ["entityId"] = "ent-001" },
            _ => new Dictionary<string, object?>()
        },
        ExpectedBehavior = "Expected behavior description"
    };

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.TrimEnd('/');
            var json = path switch
            {
                "/api/v1/discovery" => """{"datasetVersion":"v1","totalMatches":1,"entities":[{"entityId":"ent-001","name":"Test"}]}""",
                "/api/v1/aggregation" => """{"datasetVersion":"v1","metric":"count","buckets":[{"key":"cat-a","value":10}]}""",
                "/api/v1/metadata" => """{"datasetVersion":"v1","totalEntityCount":1,"totalCategoryCount":1,"dimensions":[{"name":"Category","values":[{"value":"cat-a"}]}]}""",
                "/api/v1/drill-down" => """{"datasetVersion":"v1","entity":{"entityId":"ent-001","name":"Test"}}""",
                "/api/v1/raw-evidence" => """{"datasetVersion":"v1","totalRecords":0,"records":[]}""",
                _ => """{}"""
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FailingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"code":"INTERNAL_ERROR","message":"Something went wrong"}""", Encoding.UTF8, "application/json")
            });
        }
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
