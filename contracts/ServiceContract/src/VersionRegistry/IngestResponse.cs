namespace ServiceContract.VersionRegistry;

public sealed record IngestResponse
{
    public required string VersionId { get; init; }
    public required VersionStatus Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public string? ErrorMessage { get; init; }
}
