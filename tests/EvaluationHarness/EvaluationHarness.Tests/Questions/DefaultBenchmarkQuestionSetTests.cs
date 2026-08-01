using EvaluationHarness.Questions;
using Xunit;

namespace EvaluationHarness.Tests.Questions;

public class DefaultBenchmarkQuestionSetTests
{
    [Fact]
    public void CoversAllFiveToolFamilies()
    {
        var questions = DefaultBenchmarkQuestionSet.All;
        var families = questions.Select(q => q.ToolFamily).ToHashSet();

        Assert.Contains("discovery", families);
        Assert.Contains("aggregate", families);
        Assert.Contains("explore_metadata", families);
        Assert.Contains("drill_down", families);
        Assert.Contains("get_evidence", families);
    }

    [Fact]
    public void EachQuestion_HasRequiredFields()
    {
        foreach (var q in DefaultBenchmarkQuestionSet.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(q.Id), $"Question {q.Id}: Id is required");
            Assert.False(string.IsNullOrWhiteSpace(q.ToolFamily), $"Question {q.Id}: ToolFamily is required");
            Assert.False(string.IsNullOrWhiteSpace(q.Title), $"Question {q.Id}: Title is required");
            Assert.False(string.IsNullOrWhiteSpace(q.Prompt), $"Question {q.Id}: Prompt is required");
            Assert.False(string.IsNullOrWhiteSpace(q.ToolName), $"Question {q.Id}: ToolName is required");
            Assert.NotNull(q.Arguments);
            Assert.False(string.IsNullOrWhiteSpace(q.ExpectedBehavior), $"Question {q.Id}: ExpectedBehavior is required");
        }
    }

    [Fact]
    public void EachFamily_HasAtLeastOneQuestion()
    {
        var questions = DefaultBenchmarkQuestionSet.All;
        var byFamily = questions.GroupBy(q => q.ToolFamily);

        foreach (var group in byFamily)
            Assert.True(group.Count() >= 1,
                $"Tool family '{group.Key}' has no questions");
    }

    [Fact]
    public void QuestionIds_AreUnique()
    {
        var questions = DefaultBenchmarkQuestionSet.All;
        var ids = questions.Select(q => q.Id).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public void DiscoverQuestions_HaveQueryArgument()
    {
        var discoverQuestions = DefaultBenchmarkQuestionSet.All
            .Where(q => q.ToolFamily == "discovery");

        foreach (var q in discoverQuestions)
            Assert.True(q.Arguments.ContainsKey("query"),
                $"Discovery question {q.Id} should have a 'query' argument");
    }

    [Fact]
    public void DrillDownQuestions_HaveEntityIdArgument()
    {
        var ddQuestions = DefaultBenchmarkQuestionSet.All
            .Where(q => q.ToolFamily == "drill_down");

        foreach (var q in ddQuestions)
            Assert.True(q.Arguments.ContainsKey("entityId"),
                $"Drill-down question {q.Id} should have an 'entityId' argument");
    }

    [Fact]
    public void EvidenceQuestions_HaveEntityIdArgument()
    {
        var evQuestions = DefaultBenchmarkQuestionSet.All
            .Where(q => q.ToolFamily == "get_evidence");

        foreach (var q in evQuestions)
            Assert.True(q.Arguments.ContainsKey("entityId"),
                $"Evidence question {q.Id} should have an 'entityId' argument");
    }

    [Fact]
    public void AggregateQuestions_HaveMetricArgument()
    {
        var aggQuestions = DefaultBenchmarkQuestionSet.All
            .Where(q => q.ToolFamily == "aggregate");

        foreach (var q in aggQuestions)
            Assert.True(q.Arguments.ContainsKey("metric"),
                $"Aggregate question {q.Id} should have a 'metric' argument");
    }
}
