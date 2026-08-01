namespace QueryService.Domain.Services;

public interface IDatasetVersionResolver
{
    Task<string> ResolveActiveVersionAsync(CancellationToken cancellationToken = default);
    Task<bool> VersionExistsAsync(string version, CancellationToken cancellationToken = default);
}
