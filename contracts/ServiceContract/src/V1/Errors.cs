namespace ServiceContract.V1;

public sealed record QueryError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? Detail { get; init; }
}

public static class QueryErrorCodes
{
    public const string EntityNotFound = "ENTITY_NOT_FOUND";
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string VersionNotAvailable = "VERSION_NOT_AVAILABLE";
    public const string InternalError = "INTERNAL_ERROR";
}
