using EvaluationHarness.Models;

namespace EvaluationHarness.Questions;

public static class DefaultBenchmarkQuestionSet
{
    public static IReadOnlyList<BenchmarkQuestion> All { get; } = new List<BenchmarkQuestion>
    {
        // -- Discovery (2 questions) -------------------------------------------
        new()
        {
            Id = "DISCOVER-001",
            ToolFamily = "discovery",
            Title = "Broad category search",
            Prompt = "Which cloud infrastructure companies are in the dataset?",
            ToolName = "discover",
            Arguments = new Dictionary<string, object?> { ["query"] = "cloud" },
            ExpectedBehavior = "Returns entities matching 'cloud' with relevant metadata, ranked by relevance."
        },
        new()
        {
            Id = "DISCOVER-002",
            ToolFamily = "discovery",
            Title = "Category-filtered search with pagination",
            Prompt = "Show me SaaS companies in the security space, first 5 results.",
            ToolName = "discover",
            Arguments = new Dictionary<string, object?>
            {
                ["query"] = "security",
                ["category"] = "cat-saas",
                ["limit"] = 5
            },
            ExpectedBehavior = "Returns up to 5 SaaS entities matching 'security', respecting the category filter."
        },

        // -- Aggregation (2 questions) -----------------------------------------
        new()
        {
            Id = "AGGREGATE-001",
            ToolFamily = "aggregate",
            Title = "Category distribution",
            Prompt = "What is the distribution of companies across categories?",
            ToolName = "aggregate",
            Arguments = new Dictionary<string, object?>
            {
                ["metric"] = "count",
                ["groupBy"] = "category"
            },
            ExpectedBehavior = "Returns buckets keyed by category with entity counts and percentages."
        },
        new()
        {
            Id = "AGGREGATE-002",
            ToolFamily = "aggregate",
            Title = "Sorted category distribution with limit",
            Prompt = "Which 3 categories have the most entities?",
            ToolName = "aggregate",
            Arguments = new Dictionary<string, object?>
            {
                ["metric"] = "count",
                ["groupBy"] = "category",
                ["sort"] = "count_desc",
                ["limit"] = 3
            },
            ExpectedBehavior = "Returns top 3 categories sorted by entity count descending."
        },

        // -- Metadata Exploration (2 questions) --------------------------------
        new()
        {
            Id = "METADATA-001",
            ToolFamily = "explore_metadata",
            Title = "Dataset overview",
            Prompt = "What dimensions and categories are available in this dataset?",
            ToolName = "explore_metadata",
            Arguments = new Dictionary<string, object?>(),
            ExpectedBehavior = "Returns available dimensions, categories, and entity counts."
        },
        new()
        {
            Id = "METADATA-002",
            ToolFamily = "explore_metadata",
            Title = "Specific category exploration",
            Prompt = "What is in the SaaS category?",
            ToolName = "explore_metadata",
            Arguments = new Dictionary<string, object?> { ["category"] = "cat-saas" },
            ExpectedBehavior = "Returns metadata focused on the SaaS category dimension."
        },

        // -- Drill-Down (2 questions) ------------------------------------------
        new()
        {
            Id = "DRILLDOWN-001",
            ToolFamily = "drill_down",
            Title = "Entity detail retrieval",
            Prompt = "Give me a complete profile of CloudCo Inc.",
            ToolName = "drill_down",
            Arguments = new Dictionary<string, object?> { ["entityId"] = "ent-001" },
            ExpectedBehavior = "Returns the entity with all properties, metrics, and related entities."
        },
        new()
        {
            Id = "DRILLDOWN-002",
            ToolFamily = "drill_down",
            Title = "Competitor relationship discovery",
            Prompt = "Who competes with Infra Ltd. and what are their relationships?",
            ToolName = "drill_down",
            Arguments = new Dictionary<string, object?> { ["entityId"] = "ent-002" },
            ExpectedBehavior = "Returns Infra Ltd. profile including related entities with relationship types."
        },

        // -- Raw Evidence (3 questions) ----------------------------------------
        new()
        {
            Id = "EVIDENCE-001",
            ToolFamily = "get_evidence",
            Title = "Targeted evidence retrieval",
            Prompt = "Show me pricing information for CloudCo Inc.",
            ToolName = "get_evidence",
            Arguments = new Dictionary<string, object?>
            {
                ["entityId"] = "ent-001",
                ["type"] = "pricing"
            },
            ExpectedBehavior = "Returns pricing evidence records for the specified entity."
        },
        new()
        {
            Id = "EVIDENCE-002",
            ToolFamily = "get_evidence",
            Title = "All evidence for an entity",
            Prompt = "Show me all available evidence for DataFlow Corp.",
            ToolName = "get_evidence",
            Arguments = new Dictionary<string, object?> { ["entityId"] = "ent-003" },
            ExpectedBehavior = "Returns all evidence records across types for the specified entity."
        },
        new()
        {
            Id = "EVIDENCE-003",
            ToolFamily = "get_evidence",
            Title = "Properties evidence retrieval",
            Prompt = "Show me the raw properties evidence for CloudCo Inc.",
            ToolName = "get_evidence",
            Arguments = new Dictionary<string, object?>
            {
                ["entityId"] = "ent-001",
                ["type"] = "properties"
            },
            ExpectedBehavior = "Returns the properties evidence record for the specified entity with individual property field values."
        },

        // -- Refinement-specific questions (3 questions, added Phase 4) --------
        new()
        {
            Id = "REFINE-001",
            ToolFamily = "explore_metadata",
            Title = "Discover available metrics and properties",
            Prompt = "What metrics and properties are available in this dataset?",
            ToolName = "explore_metadata",
            Arguments = new Dictionary<string, object?>(),
            ExpectedBehavior = "Returns availableMetrics and availableProperties arrays listing all metric and property keys."
        },
        new()
        {
            Id = "REFINE-002",
            ToolFamily = "aggregate",
            Title = "Statistical summary with min/max/sum",
            Prompt = "What is the average, minimum, maximum, and total revenue per category?",
            ToolName = "aggregate",
            Arguments = new Dictionary<string, object?>
            {
                ["metric"] = "revenue",
                ["groupBy"] = "category"
            },
            ExpectedBehavior = "Returns buckets with min, max, sum, and avg fields populated per category."
        },
        new()
        {
            Id = "REFINE-003",
            ToolFamily = "discovery",
            Title = "Discovery with relevance and descriptions",
            Prompt = "Find entities related to cloud and show me their descriptions ranked by relevance.",
            ToolName = "discover",
            Arguments = new Dictionary<string, object?> { ["query"] = "cloud" },
            ExpectedBehavior = "Returns entities with descriptions and relevance scores populated, ranked by score."
        }
    };
}
