using QueryService.Domain.Data;
using ServiceConfiguration;
using ServiceContract.V1;

namespace QueryService.Domain.Services;

public sealed class MetadataExplorationService : QueryServiceBase, IMetadataExplorationService
{
    public MetadataExplorationService(
        IServingStoreRepository repository,
        IDatasetVersionResolver versionResolver,
        DatasetVersionConfiguration versionConfig)
        : base(repository, versionResolver, versionConfig) { }

    public async Task<MetadataExplorationResponse> ExploreMetadataAsync(
        MetadataExplorationRequest request, CancellationToken cancellationToken = default)
    {
        var version = await ResolveVersionAsync(request.DatasetVersion, cancellationToken);

        var dimensionValues = await Repository.GetDimensionValuesAsync(version, cancellationToken);
        var totalEntities = await Repository.GetTotalEntityCountAsync(version, cancellationToken);
        var totalCategories = await Repository.GetTotalCategoryCountAsync(version, cancellationToken);
        var availableMetrics = await Repository.GetAvailableMetricsAsync(version, cancellationToken);
        var availableProperties = await Repository.GetAvailablePropertiesAsync(version, cancellationToken);

        var dimensions = new List<Dimension>();
        if (dimensionValues.Count > 0)
        {
            dimensions.Add(new Dimension
            {
                Name = "category",
                Values = dimensionValues.Select(dv => new DimensionValue
                {
                    Value = dv.Value,
                    Label = dv.Label ?? dv.Value,
                    EntityCount = dv.EntityCount
                }).ToList()
            });
        }

        return new MetadataExplorationResponse
        {
            DatasetVersion = version,
            TotalEntityCount = totalEntities,
            TotalCategoryCount = totalCategories,
            Dimensions = dimensions,
            AvailableMetrics = availableMetrics,
            AvailableProperties = availableProperties
        };
    }
}
