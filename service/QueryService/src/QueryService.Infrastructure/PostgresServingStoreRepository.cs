using Npgsql;
using QueryService.Domain.Data;

namespace QueryService.Infrastructure;

public sealed class PostgresServingStoreRepository : IServingStoreRepository, IAsyncDisposable
{
    private readonly string _connectionString;
    private NpgsqlDataSource? _dataSource;

    public PostgresServingStoreRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private NpgsqlDataSource DataSource =>
        _dataSource ??= new NpgsqlDataSourceBuilder(_connectionString).Build();

    // -- Schema -----------------------------------------------------------

    public async Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.EnsureSchema;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    // -- Version lifecycle ------------------------------------------------

    public async Task LoadVersionAsync(
        string versionId,
        IReadOnlyList<ServingEntityRow> entities,
        IReadOnlyList<ServingEvidenceRow> evidence,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        // Clear previous load of this version if re-loading
        await using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.Transaction = tx;
            clearCmd.CommandText = ServingStoreSchema.ClearVersionData;
            clearCmd.Parameters.AddWithValue("versionId", versionId);
            await clearCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Bulk-insert entities
        if (entities.Count > 0)
        {
            await using var entityCmd = connection.CreateCommand();
            entityCmd.Transaction = tx;
            entityCmd.CommandText = ServingStoreSchema.InsertEntity;

            var idParam = entityCmd.Parameters.Add("entityId", NpgsqlTypes.NpgsqlDbType.Varchar);
            var nameParam = entityCmd.Parameters.Add("name", NpgsqlTypes.NpgsqlDbType.Varchar);
            var catParam = entityCmd.Parameters.Add("category", NpgsqlTypes.NpgsqlDbType.Varchar);
            var descParam = entityCmd.Parameters.Add("description", NpgsqlTypes.NpgsqlDbType.Text);
            var propsParam = entityCmd.Parameters.Add("properties", NpgsqlTypes.NpgsqlDbType.Jsonb);
            var metricsParam = entityCmd.Parameters.Add("metrics", NpgsqlTypes.NpgsqlDbType.Jsonb);
            var verParam = entityCmd.Parameters.Add("versionId", NpgsqlTypes.NpgsqlDbType.Varchar);

            verParam.Value = versionId;

            await entityCmd.PrepareAsync(cancellationToken);

            foreach (var entity in entities)
            {
                idParam.Value = entity.EntityId;
                nameParam.Value = entity.Name;
                catParam.Value = (object?)entity.Category ?? DBNull.Value;
                descParam.Value = (object?)entity.Description ?? DBNull.Value;
                propsParam.Value = entity.PropertiesJson;
                metricsParam.Value = entity.MetricsJson;
                await entityCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        // Bulk-insert evidence
        if (evidence.Count > 0)
        {
            await using var evCmd = connection.CreateCommand();
            evCmd.Transaction = tx;
            evCmd.CommandText = ServingStoreSchema.InsertEvidence;

            var rIdParam = evCmd.Parameters.Add("recordId", NpgsqlTypes.NpgsqlDbType.Varchar);
            var eIdParam = evCmd.Parameters.Add("entityId", NpgsqlTypes.NpgsqlDbType.Varchar);
            var typeParam = evCmd.Parameters.Add("type", NpgsqlTypes.NpgsqlDbType.Varchar);
            var fieldsParam = evCmd.Parameters.Add("fields", NpgsqlTypes.NpgsqlDbType.Jsonb);
            var tsParam = evCmd.Parameters.Add("sourceTimestamp", NpgsqlTypes.NpgsqlDbType.TimestampTz);
            var verParam = evCmd.Parameters.Add("versionId", NpgsqlTypes.NpgsqlDbType.Varchar);

            verParam.Value = versionId;

            await evCmd.PrepareAsync(cancellationToken);

            foreach (var record in evidence)
            {
                rIdParam.Value = record.RecordId;
                eIdParam.Value = record.EntityId;
                typeParam.Value = record.Type;
                fieldsParam.Value = record.FieldsJson;
                tsParam.Value = (object?)record.SourceTimestamp ?? DBNull.Value;
                await evCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        // Upsert version state
        await using (var stateCmd = connection.CreateCommand())
        {
            stateCmd.Transaction = tx;
            stateCmd.CommandText = ServingStoreSchema.UpsertVersionState;
            stateCmd.Parameters.AddWithValue("versionId", versionId);
            stateCmd.Parameters.AddWithValue("entityCount", entities.Count);
            stateCmd.Parameters.AddWithValue("evidenceCount", evidence.Count);
            await stateCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    public async Task ClearVersionAsync(string versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.ClearVersionData;
        cmd.Parameters.AddWithValue("versionId", versionId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ActivateVersionAsync(string versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.ActivateVersion;
        cmd.Parameters.AddWithValue("versionId", versionId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeactivateVersionAsync(string versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.DeactivateVersion;
        cmd.Parameters.AddWithValue("versionId", versionId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string?> GetActiveVersionIdAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetActiveVersionId;
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    public async Task<ServingVersionState?> GetVersionStateAsync(
        string versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT version_id, loaded_at, entity_count, evidence_count, is_active
            FROM serving_version_state WHERE version_id = @versionId;
            """;
        cmd.Parameters.AddWithValue("versionId", versionId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new ServingVersionState
            {
                VersionId = reader.GetString(0),
                LoadedAt = reader.GetDateTime(1),
                EntityCount = reader.GetInt32(2),
                EvidenceCount = reader.GetInt32(3),
                IsActive = reader.GetBoolean(4)
            };
        }
        return null;
    }

    // -- Discovery ---------------------------------------------------------

    public async Task<IReadOnlyList<ServingEntityRow>> SearchEntitiesAsync(
        string versionId,
        string? query,
        string? category,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.SearchEntities;
        cmd.Parameters.AddWithValue("versionId", versionId);
        cmd.Parameters.AddWithValue("query", (object?)query ?? DBNull.Value);
        cmd.Parameters.AddWithValue("category", (object?)category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("offset", offset);
        cmd.Parameters.AddWithValue("limit", limit);

        return await ReadSearchEntityRowsAsync(cmd, cancellationToken);
    }

    public async Task<int> CountSearchEntitiesAsync(
        string versionId,
        string? query,
        string? category,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.CountSearchEntities;
        cmd.Parameters.AddWithValue("versionId", versionId);
        cmd.Parameters.AddWithValue("query", (object?)query ?? DBNull.Value);
        cmd.Parameters.AddWithValue("category", (object?)category ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is long count ? (int)count : 0;
    }

    // -- Drill-down --------------------------------------------------------

    public async Task<ServingEntityRow?> GetEntityByIdAsync(
        string versionId, string entityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetEntityById;
        cmd.Parameters.AddWithValue("versionId", versionId);
        cmd.Parameters.AddWithValue("entityId", entityId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
            return MapEntityRow(reader);

        return null;
    }

    public async Task<IReadOnlyList<ServingEntityRow>> GetEntitiesByCategoryAsync(
        string versionId, string category, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetEntitiesByCategory;
        cmd.Parameters.AddWithValue("versionId", versionId);
        cmd.Parameters.AddWithValue("category", category);

        return await ReadEntityRowsAsync(cmd, cancellationToken);
    }

    public async Task<IReadOnlyList<ServingEntityRow>> GetRelatedEntitiesAsync(
        string versionId, string entityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetRelatedEntities;
        cmd.Parameters.AddWithValue("versionId", versionId);
        cmd.Parameters.AddWithValue("entityId", entityId);

        return await ReadEntityRowsAsync(cmd, cancellationToken);
    }

    // -- Aggregation -------------------------------------------------------

    public async Task<IReadOnlyList<CategoryAggregateRow>> GetCategoryAggregatesAsync(
        string versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetCategoryAggregates;
        cmd.Parameters.AddWithValue("versionId", versionId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var results = new List<CategoryAggregateRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CategoryAggregateRow
            {
                Key = reader.GetString(0),
                Label = reader.IsDBNull(1) ? null : reader.GetString(1),
                Count = reader.GetInt32(2),
                Percentage = reader.GetDouble(3),
                MetricSummariesJson = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return results;
    }

    public async Task<int> GetTotalEntityCountAsync(
        string versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetTotalEntityCount;
        cmd.Parameters.AddWithValue("versionId", versionId);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is long count ? (int)count : 0;
    }

    public async Task<int> GetTotalCategoryCountAsync(
        string versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetTotalCategoryCount;
        cmd.Parameters.AddWithValue("versionId", versionId);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is long count ? (int)count : 0;
    }

    // -- Metadata exploration ----------------------------------------------

    public async Task<IReadOnlyList<DimensionValueRow>> GetDimensionValuesAsync(
        string versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetDimensionValues;
        cmd.Parameters.AddWithValue("versionId", versionId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var results = new List<DimensionValueRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new DimensionValueRow
            {
                Value = reader.GetString(0),
                Label = reader.IsDBNull(1) ? null : reader.GetString(1),
                EntityCount = reader.GetInt32(2)
            });
        }
        return results;
    }

    public async Task<IReadOnlyList<string>> GetAvailableMetricsAsync(
        string versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetAvailableMetrics;
        cmd.Parameters.AddWithValue("versionId", versionId);

        return await ReadStringListAsync(cmd, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetAvailablePropertiesAsync(
        string versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetAvailableProperties;
        cmd.Parameters.AddWithValue("versionId", versionId);

        return await ReadStringListAsync(cmd, cancellationToken);
    }

    // -- Raw evidence ------------------------------------------------------

    public async Task<IReadOnlyList<ServingEvidenceRow>> GetEvidenceByEntityAsync(
        string versionId,
        string entityId,
        string? type,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetEvidenceByEntity;
        cmd.Parameters.AddWithValue("versionId", versionId);
        cmd.Parameters.AddWithValue("entityId", entityId);
        cmd.Parameters.AddWithValue("type", (object?)type ?? DBNull.Value);
        cmd.Parameters.AddWithValue("offset", offset);
        cmd.Parameters.AddWithValue("limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var results = new List<ServingEvidenceRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ServingEvidenceRow
            {
                RecordId = reader.GetString(0),
                EntityId = reader.GetString(1),
                Type = reader.GetString(2),
                FieldsJson = reader.GetString(3),
                SourceTimestamp = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
            });
        }
        return results;
    }

    public async Task<int> CountEvidenceByEntityAsync(
        string versionId,
        string entityId,
        string? type,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.CountEvidenceByEntity;
        cmd.Parameters.AddWithValue("versionId", versionId);
        cmd.Parameters.AddWithValue("entityId", entityId);
        cmd.Parameters.AddWithValue("type", (object?)type ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is long count ? (int)count : 0;
    }

    // -- Bulk read ---------------------------------------------------------

    public async Task<IReadOnlyList<ServingEntityRow>> GetAllEntitiesByVersionAsync(
        string versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetAllEntitiesByVersion;
        cmd.Parameters.AddWithValue("versionId", versionId);
        return await ReadEntityRowsAsync(cmd, cancellationToken);
    }

    public async Task<IReadOnlyList<ServingEvidenceRow>> GetAllEvidenceByVersionAsync(
        string versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ServingStoreSchema.GetAllEvidenceByVersion;
        cmd.Parameters.AddWithValue("versionId", versionId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var results = new List<ServingEvidenceRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ServingEvidenceRow
            {
                RecordId = reader.GetString(0),
                EntityId = reader.GetString(1),
                Type = reader.GetString(2),
                FieldsJson = reader.GetString(3),
                SourceTimestamp = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
            });
        }
        return results;
    }

    // -- Helpers -----------------------------------------------------------

    private static async Task<IReadOnlyList<ServingEntityRow>> ReadEntityRowsAsync(
        NpgsqlCommand cmd, CancellationToken cancellationToken)
    {
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var results = new List<ServingEntityRow>();
        while (await reader.ReadAsync(cancellationToken))
            results.Add(MapEntityRow(reader));
        return results;
    }

    private static async Task<IReadOnlyList<string>> ReadStringListAsync(
        NpgsqlCommand cmd, CancellationToken cancellationToken)
    {
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var results = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            results.Add(reader.GetString(0));
        return results;
    }

    private static async Task<IReadOnlyList<ServingEntityRow>> ReadSearchEntityRowsAsync(
        NpgsqlCommand cmd, CancellationToken cancellationToken)
    {
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var results = new List<ServingEntityRow>();
        while (await reader.ReadAsync(cancellationToken))
            results.Add(MapSearchEntityRow(reader));
        return results;
    }

    private static ServingEntityRow MapSearchEntityRow(NpgsqlDataReader reader)
    {
        return new ServingEntityRow
        {
            EntityId = reader.GetString(0),
            Name = reader.GetString(1),
            Category = reader.IsDBNull(2) ? null : reader.GetString(2),
            Description = reader.IsDBNull(3) ? null : reader.GetString(3),
            PropertiesJson = reader.GetString(4),
            MetricsJson = reader.GetString(5),
            RelevanceScore = reader.IsDBNull(6) ? null : reader.GetDouble(6)
        };
    }

    private static ServingEntityRow MapEntityRow(NpgsqlDataReader reader)
    {
        return new ServingEntityRow
        {
            EntityId = reader.GetString(0),
            Name = reader.GetString(1),
            Category = reader.IsDBNull(2) ? null : reader.GetString(2),
            Description = reader.IsDBNull(3) ? null : reader.GetString(3),
            PropertiesJson = reader.GetString(4),
            MetricsJson = reader.GetString(5)
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();
    }
}
