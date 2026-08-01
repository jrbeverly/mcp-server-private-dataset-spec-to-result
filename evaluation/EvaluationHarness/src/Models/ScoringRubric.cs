namespace EvaluationHarness.Models;

public sealed record ScoringRubric
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<ScoringDimension> Dimensions { get; init; }

    public static ScoringRubric Default { get; } = new()
    {
        Id = "v1-default",
        Name = "Analytical Answer Quality v1",
        Description = "Evaluates whether corpus tool responses provide relevant, complete, precise, and actionable information for analyst workflows.",
        Dimensions = new List<ScoringDimension>
        {
            new()
            {
                Name = "Relevance",
                Description = "The response directly addresses the analyst question and does not include off-topic results.",
                MaxScore = 5
            },
            new()
            {
                Name = "Completeness",
                Description = "The response includes all relevant entities, categories, or evidence needed to answer the question fully.",
                MaxScore = 5
            },
            new()
            {
                Name = "Precision",
                Description = "The response is precise — not cluttered with irrelevant or low-quality matches.",
                MaxScore = 5
            },
            new()
            {
                Name = "Actionability",
                Description = "The response is structured such that the analyst can take the next investigative step without re-asking.",
                MaxScore = 5
            }
        }
    };
}

public sealed record ScoringDimension
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int MaxScore { get; init; }
}
