namespace ServiceConfiguration;

public sealed record CorpusConfiguration
{
    public required string DeploymentName { get; init; }
    public required string CorpusId { get; init; }
    public string? Description { get; init; }

    public const string SectionName = "Corpus";
}

public sealed class CorpusConfigurationLoader
{
    public static CorpusConfiguration FromEnvironment()
    {
        var deploymentName = Environment.GetEnvironmentVariable("CORPUS_DEPLOYMENT_NAME")
            ?? throw new InvalidOperationException("CORPUS_DEPLOYMENT_NAME is required");
        var corpusId = Environment.GetEnvironmentVariable("CORPUS_ID")
            ?? throw new InvalidOperationException("CORPUS_ID is required");

        return new CorpusConfiguration
        {
            DeploymentName = deploymentName,
            CorpusId = corpusId,
            Description = Environment.GetEnvironmentVariable("CORPUS_DESCRIPTION")
        };
    }
}
