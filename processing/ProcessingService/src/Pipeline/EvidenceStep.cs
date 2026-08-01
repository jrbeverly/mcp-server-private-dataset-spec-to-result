using ProcessingService.Models;

namespace ProcessingService.Pipeline;

public static class EvidenceStep
{
    public static Task<IReadOnlyList<EvidenceRecord>> GenerateAsync(
        IReadOnlyList<CanonicalEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var records = new List<EvidenceRecord>();
        var now = DateTimeOffset.UtcNow;

        foreach (var entity in entities)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entity.Properties.Count > 0)
            {
                records.Add(new EvidenceRecord
                {
                    RecordId = $"{entity.EntityId}-properties",
                    EntityId = entity.EntityId,
                    Type = "properties",
                    Fields = entity.Properties,
                    SourceTimestamp = now
                });
            }

            if (entity.Metrics.Count > 0)
            {
                records.Add(new EvidenceRecord
                {
                    RecordId = $"{entity.EntityId}-metrics",
                    EntityId = entity.EntityId,
                    Type = "metrics",
                    Fields = entity.Metrics.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture)),
                    SourceTimestamp = now
                });
            }
        }

        return Task.FromResult<IReadOnlyList<EvidenceRecord>>(records);
    }
}
