using ServiceContract.V1;

namespace QueryService.Domain.Services;

public interface IMetadataExplorationService
{
    Task<MetadataExplorationResponse> ExploreMetadataAsync(
        MetadataExplorationRequest request,
        CancellationToken cancellationToken = default);
}
