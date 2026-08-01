namespace ServiceConfiguration;

public sealed record ServiceBootstrap
{
    public required CorpusConfiguration Corpus { get; init; }
    public required StorageConfiguration Storage { get; init; }
    public required TokenConfiguration Auth { get; init; }
    public required DatasetVersionConfiguration DatasetVersion { get; init; }

    public static ServiceBootstrap Load()
    {
        return new ServiceBootstrap
        {
            Corpus = CorpusConfigurationLoader.FromEnvironment(),
            Storage = StorageConfiguration.FromEnvironment(),
            Auth = TokenConfiguration.FromEnvironment(),
            DatasetVersion = DatasetVersionConfiguration.FromEnvironment()
        };
    }
}
