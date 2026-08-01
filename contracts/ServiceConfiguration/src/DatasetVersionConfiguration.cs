namespace ServiceConfiguration;

public sealed record DatasetVersionConfiguration
{
    public string? ActiveVersion { get; init; }
    public string? DefaultVersion { get; init; }
    public bool AllowVersionPinning { get; init; }

    public const string SectionName = "DatasetVersion";

    public static DatasetVersionConfiguration FromEnvironment()
    {
        return new DatasetVersionConfiguration
        {
            ActiveVersion = Environment.GetEnvironmentVariable("DATASET_ACTIVE_VERSION"),
            DefaultVersion = Environment.GetEnvironmentVariable("DATASET_DEFAULT_VERSION"),
            AllowVersionPinning = !string.Equals(
                Environment.GetEnvironmentVariable("DATASET_ALLOW_VERSION_PINNING"),
                "false",
                StringComparison.OrdinalIgnoreCase)
        };
    }
}
