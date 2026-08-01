using System.Text.Json;
using EvaluationHarness.Models;
using EvaluationHarness.Storage;

namespace EvaluationHarness.Scoring;

public sealed class RubricScorer
{
    private readonly IResultStore _store;

    public RubricScorer(IResultStore store)
    {
        _store = store;
    }

    public async Task<ScoredRun> AutoScoreAsync(
        string runId, ScoringRubric rubric, CancellationToken ct = default)
    {
        var run = await _store.LoadAsync(runId, ct)
            ?? throw new InvalidOperationException($"Run not found: {runId}");

        var scores = new List<ScoredQuestion>(run.Results.Count);
        foreach (var result in run.Results)
        {
            var heuristicScores = ComputeHeuristicScores(result, rubric);
            var failureCategory = ClassifyFailure(result, heuristicScores);
            scores.Add(new ScoredQuestion
            {
                QuestionId = result.QuestionId,
                ToolFamily = result.ToolFamily,
                Dimensions = heuristicScores,
                FailureCategory = failureCategory,
                Notes = failureCategory != FailureCategories.None
                    ? $"Auto-flagged: {failureCategory}. {GetFailureNote(result, failureCategory)}"
                    : null
            });
        }

        var dimensionNames = rubric.Dimensions.Select(d => d.Name).ToList();
        return new ScoredRun
        {
            RunId = runId,
            ScoredAt = DateTimeOffset.UtcNow.ToString("O"),
            RubricId = rubric.Id,
            Scores = scores,
            Summary = new ScoredRunSummary
            {
                TotalQuestions = scores.Count,
                OverallAverage = scores.Average(s =>
                    s.Dimensions.Average(d => (double)d.Score)),
                AveragesByDimension = dimensionNames.ToDictionary(
                    name => name,
                    name => scores.Average(s =>
                        (double)(s.Dimensions.FirstOrDefault(d => d.DimensionName == name)?.Score ?? 0))),
                AveragesByToolFamily = scores
                    .GroupBy(s => s.ToolFamily)
                    .ToDictionary(g => g.Key, g => g.Average(s =>
                        s.Dimensions.Average(d => (double)d.Score)))
            }
        };
    }

    public RunComparisonReport Compare(
        ScoredRun left,
        string leftLabel,
        ScoredRun right,
        string rightLabel)
    {
        var leftScoresByQuestion = left.Scores.ToDictionary(s => s.QuestionId);
        var rightScoresByQuestion = right.Scores.ToDictionary(s => s.QuestionId);

        var allQuestionIds = leftScoresByQuestion.Keys
            .Union(rightScoresByQuestion.Keys)
            .OrderBy(id => id)
            .ToList();

        var comparisons = new List<QuestionComparison>();
        int improved = 0, regressed = 0, unchanged = 0, unscored = 0;
        double leftSum = 0, rightSum = 0;
        int scoredCount = 0;

        foreach (var qid in allQuestionIds)
        {
            var hasLeft = leftScoresByQuestion.TryGetValue(qid, out var leftQ);
            var hasRight = rightScoresByQuestion.TryGetValue(qid, out var rightQ);

            if (!hasLeft || !hasRight)
            {
                unscored++;
                comparisons.Add(new QuestionComparison
                {
                    QuestionId = qid,
                    ToolFamily = (leftQ ?? rightQ)!.ToolFamily,
                    Note = "Missing from one side"
                });
                continue;
            }

            var leftAvg = leftQ!.Dimensions.Average(d => (double)d.Score);
            var rightAvg = rightQ!.Dimensions.Average(d => (double)d.Score);
            var delta = rightAvg - leftAvg;

            leftSum += leftAvg;
            rightSum += rightAvg;
            scoredCount++;

            if (Math.Abs(delta) < 0.01)
            {
                unchanged++;
            }
            else if (delta > 0)
            {
                improved++;
            }
            else
            {
                regressed++;
            }

            comparisons.Add(new QuestionComparison
            {
                QuestionId = qid,
                ToolFamily = leftQ.ToolFamily,
                LeftAverageScore = leftAvg,
                RightAverageScore = rightAvg,
                ScoreDelta = delta,
                Regressed = delta < -0.01,
                Improved = delta > 0.01,
                Note = DetermineDeltaNote(leftQ, rightQ, delta)
            });
        }

        return new RunComparisonReport
        {
            LeftRunId = left.RunId,
            LeftRunLabel = leftLabel,
            RightRunId = right.RunId,
            RightRunLabel = rightLabel,
            ComparedAt = DateTimeOffset.UtcNow.ToString("O"),
            QuestionComparisons = comparisons,
            Summary = new ComparisonSummary
            {
                TotalQuestions = allQuestionIds.Count,
                Improved = improved,
                Regressed = regressed,
                Unchanged = unchanged,
                Unscored = unscored,
                OverallLeftAverage = scoredCount > 0 ? leftSum / scoredCount : null,
                OverallRightAverage = scoredCount > 0 ? rightSum / scoredCount : null,
                OverallDelta = scoredCount > 0 ? (rightSum - leftSum) / scoredCount : null
            }
        };
    }

    private static List<DimensionScore> ComputeHeuristicScores(
        QuestionResult result, ScoringRubric rubric)
    {
        var scores = new List<DimensionScore>();

        foreach (var dimension in rubric.Dimensions)
        {
            var (score, comment) = dimension.Name switch
            {
                "Relevance" => ScoreRelevance(result),
                "Completeness" => ScoreCompleteness(result),
                "Precision" => ScorePrecision(result),
                "Actionability" => ScoreActionability(result),
                _ => (dimension.MaxScore / 2, "No heuristic; requires manual review.")
            };

            scores.Add(new DimensionScore
            {
                DimensionName = dimension.Name,
                Score = Math.Clamp(score, 1, dimension.MaxScore),
                Comment = comment
            });
        }

        return scores;
    }

    private static (int score, string comment) ScoreRelevance(QuestionResult result)
    {
        if (result.Status == "failed")
            return (1, "Tool call failed — response is not relevant.");

        if (result.Response is null)
            return (1, "No response returned.");

        return (4, "Response received; manual review recommended to verify relevance.");
    }

    private static (int score, string comment) ScoreCompleteness(QuestionResult result)
    {
        if (result.Status == "failed")
            return (1, "Tool call failed — no data returned.");

        if (result.Response is null)
            return (1, "No response returned.");

        var (hasContent, bonusCount) = result.ToolFamily switch
        {
            "discovery" => (
                HasResponseField(result.Response.Value, "entities"),
                HasResponseField(result.Response.Value, "entities/0/description") ? 1 : 0
                    + (HasResponseField(result.Response.Value, "entities/0/relevanceScore") ? 1 : 0)
            ),
            "aggregate" => (
                HasResponseField(result.Response.Value, "buckets"),
                HasResponseField(result.Response.Value, "buckets/0/min") ? 1 : 0
                    + (HasResponseField(result.Response.Value, "buckets/0/max") ? 1 : 0)
                    + (HasResponseField(result.Response.Value, "buckets/0/sum") ? 1 : 0)
            ),
            "drill_down" => (
                HasResponseField(result.Response.Value, "entity"),
                0
            ),
            "explore_metadata" => (
                HasResponseField(result.Response.Value, "dimensions"),
                HasResponseField(result.Response.Value, "availableMetrics") ? 1 : 0
                    + (HasResponseField(result.Response.Value, "availableProperties") ? 1 : 0)
            ),
            "get_evidence" => (
                HasResponseField(result.Response.Value, "records"),
                0
            ),
            _ => (true, 0)
        };

        if (!hasContent)
            return (2, "Response is missing expected data fields — corpus may be incomplete.");

        var score = 4 + Math.Min(bonusCount, 1);
        return bonusCount > 0
            ? (score, $"Expected data fields present. Refinement fields detected (+{bonusCount}).")
            : (4, "Expected data fields present.");
    }

    private static bool HasResponseField(JsonElement response, string path)
    {
        var parts = path.Split('/');
        var current = response;

        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return false;

            // Handle array index notation: "entities/0/description"
            if (int.TryParse(part, out var index))
            {
                if (current.ValueKind != JsonValueKind.Array)
                    return false;

                using var enumerator = current.EnumerateArray().GetEnumerator();
                for (var i = 0; i <= index; i++)
                {
                    if (!enumerator.MoveNext())
                        return false;
                }
                current = enumerator.Current;
            }
            else if (!current.TryGetProperty(part, out var prop))
            {
                return false;
            }
            else
            {
                current = prop;
            }
        }

        return true;
    }

    private static (int score, string comment) ScorePrecision(QuestionResult result)
    {
        if (result.Status == "failed" || result.Response is null)
            return (1, "No data to evaluate for precision.");

        return (4, "Response received; manual review recommended to verify precision.");
    }

    private static (int score, string comment) ScoreActionability(QuestionResult result)
    {
        if (result.Status == "failed")
            return (1, "Tool call failed — no actionable data.");

        if (result.Response is null)
            return (1, "No response returned.");

        return (4, "Structured response returned; manual review recommended for actionability.");
    }

    private static string ClassifyFailure(
        QuestionResult result, List<DimensionScore> scores)
    {
        if (result.Status == "failed")
            return FailureCategories.ToolSurface;

        if (result.Response is null)
            return FailureCategories.ResponseShaping;

        var avg = scores.Average(s => (double)s.Score);
        if (avg < 2.5)
            return FailureCategories.CorpusQuality;

        return FailureCategories.None;
    }

    private static string GetFailureNote(QuestionResult result, string category)
    {
        return category switch
        {
            FailureCategories.ToolSurface =>
                result.ErrorMessage is not null
                    ? $"Tool error: {result.ErrorMessage}"
                    : "Tool call failed with no error message.",

            FailureCategories.CorpusQuality =>
                "Response received but appears degraded — verify corpus quality for this dataset version.",

            FailureCategories.ResponseShaping =>
                "Response was missing or empty — the service may not be returning well-formed data.",

            _ => string.Empty
        };
    }

    private static string? DetermineDeltaNote(
        ScoredQuestion left, ScoredQuestion right, double delta)
    {
        if (delta < -0.5)
        {
            // Check which dimensions regressed
            var regressed = right.Dimensions
                .Where(rd => left.Dimensions.Any(ld =>
                    ld.DimensionName == rd.DimensionName && ld.Score > rd.Score))
                .Select(rd => rd.DimensionName)
                .ToList();

            if (regressed.Count > 0)
                return $"Regressed in: {string.Join(", ", regressed)}.";

            return "Overall score regression.";
        }

        if (delta > 0.5)
        {
            var improved = right.Dimensions
                .Where(rd => left.Dimensions.Any(ld =>
                    ld.DimensionName == rd.DimensionName && ld.Score < rd.Score))
                .Select(rd => rd.DimensionName)
                .ToList();

            if (improved.Count > 0)
                return $"Improved in: {string.Join(", ", improved)}.";

            return "Overall score improvement.";
        }

        return null;
    }
}
