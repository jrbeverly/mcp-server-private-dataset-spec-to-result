using System.Text.Json;
using QueryService.Domain.Data;
using QueryService.Domain.Exceptions;
using ServiceConfiguration;
using ServiceContract.V1;

namespace QueryService.Domain.Services;

public sealed class DrillDownService : QueryServiceBase, IDrillDownService
{
    public DrillDownService(
        IServingStoreRepository repository,
        IDatasetVersionResolver versionResolver,
        DatasetVersionConfiguration versionConfig)
        : base(repository, versionResolver, versionConfig) { }

    public async Task<DrillDownResponse> DrillDownAsync(
        DrillDownRequest request, CancellationToken cancellationToken = default)
    {
        var version = await ResolveVersionAsync(request.DatasetVersion, cancellationToken);

        var entity = await Repository.GetEntityByIdAsync(version, request.EntityId, cancellationToken);
        if (entity is null)
            throw new NotFoundException("Entity", request.EntityId);

        var related = await Repository.GetRelatedEntitiesAsync(version, request.EntityId, cancellationToken);

        return new DrillDownResponse
        {
            DatasetVersion = version,
            Entity = MapToDetail(entity),
            RelatedEntities = related.Select(MapToRelated).ToList()
        };
    }

    private static EntityDetail MapToDetail(ServingEntityRow row)
    {
        return new EntityDetail
        {
            EntityId = row.EntityId,
            Name = row.Name,
            Category = row.Category,
            Description = row.Description,
            Properties = DeserializeDictionary(row.PropertiesJson),
            Metrics = DeserializeDecimalDictionary(row.MetricsJson)
        };
    }

    private static RelatedEntity MapToRelated(ServingEntityRow row)
    {
        return new RelatedEntity
        {
            EntityId = row.EntityId,
            Name = row.Name,
            RelationshipType = "category",
            Category = row.Category
        };
    }

    private static IReadOnlyDictionary<string, string>? DeserializeDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, decimal>? DeserializeDecimalDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, decimal>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
