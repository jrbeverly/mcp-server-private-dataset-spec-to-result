using ProcessingService.Models;

namespace ProcessingService.Pipeline;

public static class EnrichmentStep
{
    public static async Task<IReadOnlyList<CanonicalEntity>> EnrichAsync(
        IReadOnlyList<CanonicalEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var enriched = new List<CanonicalEntity>(entities.Count);

        foreach (var entity in entities)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var enrichedEntity = entity with
            {
                Properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase),
                Metrics = new Dictionary<string, decimal>(entity.Metrics, StringComparer.OrdinalIgnoreCase)
            };

            // Derive a "total" metric if multiple metrics exist
            if (enrichedEntity.Metrics.Count > 1 && !enrichedEntity.Metrics.ContainsKey("total"))
            {
                enrichedEntity.Metrics["total"] = enrichedEntity.Metrics.Values.Sum();
            }

            // Derive a "count" metric from property count
            enrichedEntity.Metrics["property_count"] = enrichedEntity.Properties.Count;

            // Add entity_id as a property for searchability
            enrichedEntity.Properties["_entity_id"] = enrichedEntity.EntityId;

            enriched.Add(enrichedEntity);
        }

        await Task.CompletedTask;
        return enriched;
    }
}
