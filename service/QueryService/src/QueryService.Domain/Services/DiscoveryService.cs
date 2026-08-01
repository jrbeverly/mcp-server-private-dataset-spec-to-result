using QueryService.Domain.Data;
using ServiceConfiguration;
using ServiceContract.V1;

namespace QueryService.Domain.Services;

public sealed class DiscoveryService : QueryServiceBase, IDiscoveryService
{
    public DiscoveryService(
        IServingStoreRepository repository,
        IDatasetVersionResolver versionResolver,
        DatasetVersionConfiguration versionConfig)
        : base(repository, versionResolver, versionConfig) { }

    public async Task<DiscoveryResponse> DiscoverAsync(
        DiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        var version = await ResolveVersionAsync(request.DatasetVersion, cancellationToken);
        var offset = request.Offset ?? 0;
        var limit = request.Limit ?? 20;

        var entities = await Repository.SearchEntitiesAsync(
            version, request.Query, request.Category, offset, limit, cancellationToken);
        var total = await Repository.CountSearchEntitiesAsync(
            version, request.Query, request.Category, cancellationToken);

        return new DiscoveryResponse
        {
            DatasetVersion = version,
            TotalMatches = total,
            Entities = entities.Select(Map).ToList()
        };
    }

    private static DiscoveredEntity Map(ServingEntityRow row)
    {
        return new DiscoveredEntity
        {
            EntityId = row.EntityId,
            Name = row.Name,
            Category = row.Category,
            Description = row.Description,
            RelevanceScore = row.RelevanceScore.HasValue
                ? (decimal?)Math.Round((decimal)row.RelevanceScore.Value, 4)
                : null
        };
    }
}
