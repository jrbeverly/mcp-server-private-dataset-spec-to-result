using System.Text.Json;
using EvaluationHarness.Models;
using EvaluationHarness.Questions;
using EvaluationHarness.Runner;
using EvaluationHarness.Scoring;
using EvaluationHarness.Storage;

var cliArgs = Environment.GetCommandLineArgs();
var command = cliArgs.Length > 1 ? cliArgs[1].ToLowerInvariant() : "help";

try
{
    switch (command)
    {
        case "run":
            await RunBenchmarkAsync(cliArgs.Skip(2).ToArray());
            break;
        case "score":
            await ScoreRunAsync(cliArgs.Skip(2).ToArray());
            break;
        case "compare":
            await CompareRunsAsync(cliArgs.Skip(2).ToArray());
            break;
        case "list":
            await ListRunsAsync(cliArgs.Skip(2).ToArray());
            break;
        default:
            PrintHelp();
            break;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}

static async Task RunBenchmarkAsync(string[] args)
{
    var serviceUrl = ParseArg(args, "--service-url", "-s")
        ?? Environment.GetEnvironmentVariable("QUERY_SERVICE_URL")
        ?? "http://localhost:5000";

    var token = ParseArg(args, "--token", "-t")
        ?? Environment.GetEnvironmentVariable("AUTH_TOKEN")
        ?? "dev-token";

    var label = ParseArg(args, "--label", "-l") ?? "benchmark-run";
    var outputDir = ParseArg(args, "--output", "-o")
        ?? Environment.GetEnvironmentVariable("EVAL_RESULTS_DIR")
        ?? Path.Combine(Path.GetTempPath(), "eval-results");

    Console.WriteLine($"Running benchmark against {serviceUrl}");
    Console.WriteLine($"Output directory: {outputDir}");
    Console.WriteLine();

    var httpClient = new HttpClient { BaseAddress = new Uri(serviceUrl.TrimEnd('/')) };
    httpClient.DefaultRequestHeaders.Add("X-Api-Token", token);

    var adapter = new QueryServiceAdapter(httpClient, serviceUrl);
    var store = new FileSystemResultStore(outputDir);
    var runner = new BenchmarkRunner(adapter, store);

    var result = await runner.RunAsync(label, DefaultBenchmarkQuestionSet.All);

    Console.WriteLine($"Run ID:    {result.RunId}");
    Console.WriteLine($"Questions: {result.Summary?.TotalQuestions}");
    Console.WriteLine($"Passed:    {result.Summary?.Passed}");
    Console.WriteLine($"Failed:    {result.Summary?.Failed}");
    Console.WriteLine($"Avg Lat:   {result.Summary?.AverageLatencyMs:F1}ms");

    if (result.Summary?.Failed > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Failures:");
        foreach (var r in result.Results.Where(r => r.Status == "failed"))
            Console.WriteLine($"  [{r.QuestionId}] {r.ErrorMessage}");
    }
}

static async Task ScoreRunAsync(string[] args)
{
    var resultsDir = ParseArg(args, "--results-dir", "-d")
        ?? Environment.GetEnvironmentVariable("EVAL_RESULTS_DIR")
        ?? Path.Combine(Path.GetTempPath(), "eval-results");

    var store = new FileSystemResultStore(resultsDir);
    var scorer = new RubricScorer(store);

    var runId = ParseArg(args, "--run", "-r");
    if (runId is null)
    {
        // Score the most recent run if not specified
        var ids = await store.ListRunIdsAsync();
        runId = ids.FirstOrDefault()
            ?? throw new InvalidOperationException("No runs found. Run a benchmark first with 'run'.");
        Console.WriteLine($"Scoring most recent run: {runId}");
    }

    var scored = await scorer.AutoScoreAsync(runId, ScoringRubric.Default);

    // Save scored result alongside the run
    var scoredPath = Path.Combine(resultsDir, $"{runId}.scored.json");
    var json = JsonSerializer.Serialize(scored, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    });
    await File.WriteAllTextAsync(scoredPath, json);

    Console.WriteLine($"Scored run: {runId}");
    Console.WriteLine($"Rubric:     {scored.RubricId}");
    Console.WriteLine($"Overall:    {scored.Summary?.OverallAverage:F2} / 5.00");
    Console.WriteLine();
    Console.WriteLine("By dimension:");
    if (scored.Summary?.AveragesByDimension is not null)
        foreach (var (dim, avg) in scored.Summary.AveragesByDimension)
            Console.WriteLine($"  {dim,-20} {avg:F2}");

    Console.WriteLine();
    Console.WriteLine("By tool family:");
    if (scored.Summary?.AveragesByToolFamily is not null)
        foreach (var (family, avg) in scored.Summary.AveragesByToolFamily)
            Console.WriteLine($"  {family,-20} {avg:F2}");

    // Surface failures
    var failures = scored.Scores
        .Where(s => s.FailureCategory != FailureCategories.None)
        .ToList();

    if (failures.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Issues surfaced:");
        foreach (var f in failures)
            Console.WriteLine($"  [{f.QuestionId}] [{f.FailureCategory}] {f.Notes}");
    }
}

static async Task CompareRunsAsync(string[] args)
{
    var resultsDir = ParseArg(args, "--results-dir", "-d")
        ?? Environment.GetEnvironmentVariable("EVAL_RESULTS_DIR")
        ?? Path.Combine(Path.GetTempPath(), "eval-results");

    var leftId = ParseArg(args, "--left", "-l")
        ?? throw new InvalidOperationException("--left run ID is required for comparison");
    var rightId = ParseArg(args, "--right", "-r")
        ?? throw new InvalidOperationException("--right run ID is required for comparison");

    var store = new FileSystemResultStore(resultsDir);
    var scorer = new RubricScorer(store);

    // Load scored versions, or auto-score raw runs
    var leftScored = await LoadOrScoreAsync(store, scorer, leftId);
    var rightScored = await LoadOrScoreAsync(store, scorer, rightId);

    var leftRun = await store.LoadAsync(leftId);
    var rightRun = await store.LoadAsync(rightId);

    var report = scorer.Compare(
        leftScored, leftRun?.RunLabel ?? leftId,
        rightScored, rightRun?.RunLabel ?? rightId);

    Console.WriteLine($"Comparison: {report.LeftRunLabel} vs {report.RightRunLabel}");
    Console.WriteLine();
    Console.WriteLine($"Left:  {report.Summary.OverallLeftAverage:F2} ({report.LeftRunId})");
    Console.WriteLine($"Right: {report.Summary.OverallRightAverage:F2} ({report.RightRunId})");
    Console.WriteLine($"Delta: {report.Summary.OverallDelta:+#.##;-#.##;0.00}");
    Console.WriteLine();
    Console.WriteLine($"Improved:  {report.Summary.Improved}");
    Console.WriteLine($"Regressed: {report.Summary.Regressed}");
    Console.WriteLine($"Unchanged: {report.Summary.Unchanged}");
    Console.WriteLine($"Unscored:  {report.Summary.Unscored}");

    var regressions = report.QuestionComparisons
        .Where(c => c.Regressed == true)
        .ToList();

    if (regressions.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Regressions:");
        foreach (var r in regressions)
            Console.WriteLine($"  [{r.QuestionId}] {r.ToolFamily} — delta: {r.ScoreDelta:F2} — {r.Note}");
    }

    var improvements = report.QuestionComparisons
        .Where(c => c.Improved == true)
        .ToList();

    if (improvements.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Improvements:");
        foreach (var i in improvements)
            Console.WriteLine($"  [{i.QuestionId}] {i.ToolFamily} — delta: {i.ScoreDelta:F2} — {i.Note}");
    }

    // Save comparison report
    var comparisonPath = Path.Combine(resultsDir, $"compare-{leftId}-vs-{rightId}.json");
    var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    });
    await File.WriteAllTextAsync(comparisonPath, json);
    Console.WriteLine();
    Console.WriteLine($"Report saved: {comparisonPath}");
}

static async Task ListRunsAsync(string[] args)
{
    var resultsDir = ParseArg(args, "--results-dir", "-d")
        ?? Environment.GetEnvironmentVariable("EVAL_RESULTS_DIR")
        ?? Path.Combine(Path.GetTempPath(), "eval-results");

    var store = new FileSystemResultStore(resultsDir);
    var ids = await store.ListRunIdsAsync();

    if (ids.Count == 0)
    {
        Console.WriteLine("No runs found.");
        return;
    }

    Console.WriteLine($"Found {ids.Count} run(s):");
    foreach (var id in ids)
    {
        var run = await store.LoadAsync(id);
        Console.WriteLine($"  {id}  [{run?.RunLabel}]  {run?.Summary?.Passed}/{run?.Summary?.TotalQuestions} passed");
    }
}

static async Task<ScoredRun> LoadOrScoreAsync(
    FileSystemResultStore store, RubricScorer scorer, string runId)
{
    var scoredPath = Path.Combine(
        Environment.GetEnvironmentVariable("EVAL_RESULTS_DIR")
            ?? Path.Combine(Path.GetTempPath(), "eval-results"),
        $"{runId}.scored.json");

    if (File.Exists(scoredPath))
    {
        var json = await File.ReadAllTextAsync(scoredPath);
        return JsonSerializer.Deserialize<ScoredRun>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })!;
    }

    return await scorer.AutoScoreAsync(runId, ScoringRubric.Default);
}

static string? ParseArg(string[] args, string longForm, string shortForm)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], longForm, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[i], shortForm, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return null;
}

static void PrintHelp()
{
    Console.WriteLine("Evaluation Harness — Benchmark scoring workflow");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  run      Execute benchmark questions against the query service");
    Console.WriteLine("  score    Apply rubric-based scoring to a benchmark run");
    Console.WriteLine("  compare  Compare two scored benchmark runs");
    Console.WriteLine("  list     List saved benchmark runs");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run -- run   [--service-url <url>] [--token <token>] [--label <label>] [--output <dir>]");
    Console.WriteLine("  dotnet run -- score [--run <run-id>] [--results-dir <dir>]");
    Console.WriteLine("  dotnet run -- compare --left <run-id> --right <run-id> [--results-dir <dir>]");
    Console.WriteLine("  dotnet run -- list  [--results-dir <dir>]");
    Console.WriteLine();
    Console.WriteLine("Environment variables:");
    Console.WriteLine("  QUERY_SERVICE_URL  Default: http://localhost:5000");
    Console.WriteLine("  AUTH_TOKEN         Default: dev-token");
    Console.WriteLine("  EVAL_RESULTS_DIR   Default: /tmp/eval-results");
}
