using QueryService.Domain.Data;
using QueryService.Domain.Services;
using ServiceConfiguration;

namespace QueryService.Infrastructure;

public sealed class DatasetVersionResolver : IDatasetVersionResolver
{
    private readonly IServingStoreRepository _repository;
    private readonly DatasetVersionConfiguration _config;

    public DatasetVersionResolver(
        IServingStoreRepository repository,
        DatasetVersionConfiguration config)
    {
        _repository = repository;
        _config = config;
    }

    public async Task<string> ResolveActiveVersionAsync(CancellationToken cancellationToken = default)
    {
        // If the operator has pinned a specific active version via config, use it
        if (!string.IsNullOrWhiteSpace(_config.ActiveVersion))
            return _config.ActiveVersion;

        // Otherwise resolve the version that's activated in the serving store
        var active = await _repository.GetActiveVersionIdAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(active))
            return active;

        throw new InvalidOperationException(
            "No active dataset version is configured and no version is activated in the serving store. " +
            "Load and activate a version first.");
    }

    public async Task<bool> VersionExistsAsync(string version, CancellationToken cancellationToken = default)
    {
        var state = await _repository.GetVersionStateAsync(version, cancellationToken);
        return state is not null;
    }
}
