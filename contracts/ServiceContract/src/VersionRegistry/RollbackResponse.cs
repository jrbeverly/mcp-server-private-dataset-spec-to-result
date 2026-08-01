namespace ServiceContract.VersionRegistry;

public sealed record RollbackResponse
{
    public required string PreviousActiveVersionId { get; init; }
    public required string NewActiveVersionId { get; init; }
}
