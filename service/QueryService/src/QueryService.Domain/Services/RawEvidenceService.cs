using System.Text.Json;
using QueryService.Domain.Data;
using ServiceConfiguration;
using ServiceContract.V1;

namespace QueryService.Domain.Services;

public sealed class RawEvidenceService : QueryServiceBase, IRawEvidenceService
{
    public RawEvidenceService(
        IServingStoreRepository repository,
        IDatasetVersionResolver versionResolver,
        DatasetVersionConfiguration versionConfig)
        : base(repository, versionResolver, versionConfig) { }

    public async Task<RawEvidenceResponse> GetRawEvidenceAsync(
        RawEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        var version = await ResolveVersionAsync(request.DatasetVersion, cancellationToken);
        var offset = request.Offset ?? 0;
        var limit = request.Limit ?? 20;

        var records = await Repository.GetEvidenceByEntityAsync(
            version, request.EntityId, request.EvidenceType, offset, limit, cancellationToken);
        var total = await Repository.CountEvidenceByEntityAsync(
            version, request.EntityId, request.EvidenceType, cancellationToken);

        return new RawEvidenceResponse
        {
            DatasetVersion = version,
            TotalRecords = total,
            Records = records.Select(Map).ToList()
        };
    }

    private static EvidenceRecord Map(ServingEvidenceRow row)
    {
        return new EvidenceRecord
        {
            RecordId = row.RecordId,
            EntityId = row.EntityId,
            Type = row.Type,
            Fields = DeserializeFields(row.FieldsJson),
            SourceTimestamp = row.SourceTimestamp
        };
    }

    private static IReadOnlyDictionary<string, string> DeserializeFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }
}
