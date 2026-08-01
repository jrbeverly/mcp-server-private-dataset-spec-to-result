namespace ServiceContract.VersionRegistry;

public sealed record ListVersionsResponse
{
    public required IReadOnlyList<DatasetVersionRecord> Versions { get; init; }
}
