using QueryService.Domain.Data;
using QueryService.Domain.Exceptions;
using ServiceConfiguration;

namespace QueryService.Domain.Services;

public abstract class QueryServiceBase
{
    protected IServingStoreRepository Repository { get; }
    protected IDatasetVersionResolver VersionResolver { get; }
    protected DatasetVersionConfiguration VersionConfig { get; }

    protected QueryServiceBase(
        IServingStoreRepository repository,
        IDatasetVersionResolver versionResolver,
        DatasetVersionConfiguration versionConfig)
    {
        Repository = repository;
        VersionResolver = versionResolver;
        VersionConfig = versionConfig;
    }

    protected async Task<string> ResolveVersionAsync(
        string? requestedVersion,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedVersion))
        {
            if (!VersionConfig.AllowVersionPinning)
                throw new BadRequestException(
                    "Version pinning is not enabled for this deployment.");

            if (!await VersionResolver.VersionExistsAsync(requestedVersion, cancellationToken))
                throw new VersionNotAvailableException(requestedVersion);

            return requestedVersion;
        }

        return await VersionResolver.ResolveActiveVersionAsync(cancellationToken);
    }
}
