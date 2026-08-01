namespace ServiceContract.VersionRegistry;

public sealed record IngestRequest
{
    public required string SourcePath { get; init; }
    public string? Metadata { get; init; }
}
