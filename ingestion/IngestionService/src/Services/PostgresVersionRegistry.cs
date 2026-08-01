using System.Text.Json;
using Npgsql;
using ServiceContract.VersionRegistry;

namespace IngestionService.Services;

public sealed class PostgresVersionRegistry : IVersionRegistry, IAsyncDisposable
{
    private readonly string _connectionString;
    private NpgsqlDataSource? _dataSource;

    public PostgresVersionRegistry(string connectionString)
    {
        _connectionString = connectionString;
    }

    private NpgsqlDataSource DataSource =>
        _dataSource ??= NpgsqlDataSource.Create(_connectionString);

    public async Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS dataset_versions (
                version_id VARCHAR(64) PRIMARY KEY,
                status VARCHAR(32) NOT NULL DEFAULT 'Raw',
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                source_path TEXT,
                raw_artifact_path TEXT,
                processed_artifact_path TEXT,
                metadata JSONB,
                previous_active_version_id VARCHAR(64),
                replaced_by_version_id VARCHAR(64),
                error_message TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_dataset_versions_status
                ON dataset_versions(status);

            CREATE INDEX IF NOT EXISTS idx_dataset_versions_created_at
                ON dataset_versions(created_at DESC);
            """;

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DatasetVersionRecord> RegisterRawVersionAsync(
        string versionId,
        string sourcePath,
        string? rawArtifactPath,
        string? metadata,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO dataset_versions
                (version_id, status, created_at, updated_at, source_path, raw_artifact_path, metadata)
            VALUES
                ($1, $2, $3, $4, $5, $6, $7);
            """;

        cmd.Parameters.AddWithValue(versionId);
        cmd.Parameters.AddWithValue(VersionStatus.Raw.ToString());
        cmd.Parameters.AddWithValue(now);
        cmd.Parameters.AddWithValue(now);
        cmd.Parameters.AddWithValue((object?)sourcePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)rawArtifactPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue(metadata is not null ? JsonSerializer.Serialize(metadata) : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return new DatasetVersionRecord
        {
            VersionId = versionId,
            Status = VersionStatus.Raw,
            CreatedAt = now,
            UpdatedAt = now,
            SourcePath = sourcePath,
            RawArtifactPath = rawArtifactPath,
            Metadata = metadata
        };
    }

    public async Task MarkVersionStatusAsync(
        string versionId,
        VersionStatus status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE dataset_versions
            SET status = $1, updated_at = $2, error_message = $3
            WHERE version_id = $4;
            """;

        cmd.Parameters.AddWithValue(status.ToString());
        cmd.Parameters.AddWithValue(now);
        cmd.Parameters.AddWithValue((object?)errorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue(versionId);

        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);

        if (rows == 0)
            throw new InvalidOperationException($"Version '{versionId}' not found");
    }

    public async Task UpdateProcessedArtifactAsync(
        string versionId,
        string processedArtifactPath,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE dataset_versions
            SET processed_artifact_path = $1, updated_at = $2
            WHERE version_id = $3;
            """;

        cmd.Parameters.AddWithValue(processedArtifactPath);
        cmd.Parameters.AddWithValue(now);
        cmd.Parameters.AddWithValue(versionId);

        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);

        if (rows == 0)
            throw new InvalidOperationException($"Version '{versionId}' not found");
    }

    public async Task<RollbackResponse> RollbackToVersionAsync(
        string targetVersionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        // Verify the target version exists and is in a rollback-eligible status
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.Transaction = tx;
            checkCmd.CommandText = """
                SELECT status FROM dataset_versions WHERE version_id = $1;
                """;
            checkCmd.Parameters.AddWithValue(targetVersionId);

            var result = await checkCmd.ExecuteScalarAsync(cancellationToken);
            if (result is null)
                throw new InvalidOperationException($"Target version '{targetVersionId}' does not exist");

            var status = Enum.Parse<VersionStatus>((string)result);
            if (status is not (VersionStatus.Published or VersionStatus.Active or VersionStatus.Processed))
                throw new InvalidOperationException(
                    $"Version '{targetVersionId}' is not eligible for rollback (status: {status})");
        }

        // Find the current active version
        string? currentActiveId = null;
        await using (var findCmd = connection.CreateCommand())
        {
            findCmd.Transaction = tx;
            findCmd.CommandText = """
                SELECT version_id FROM dataset_versions
                WHERE status = 'Active'
                LIMIT 1;
                """;

            currentActiveId = (string?)await findCmd.ExecuteScalarAsync(cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;

        // Supersede the current active version
        if (currentActiveId is not null)
        {
            await using var supersedeCmd = connection.CreateCommand();
            supersedeCmd.Transaction = tx;
            supersedeCmd.CommandText = """
                UPDATE dataset_versions
                SET status = 'Superseded',
                    updated_at = $1,
                    replaced_by_version_id = $2
                WHERE version_id = $3;
                """;
            supersedeCmd.Parameters.AddWithValue(now);
            supersedeCmd.Parameters.AddWithValue(targetVersionId);
            supersedeCmd.Parameters.AddWithValue(currentActiveId);
            await supersedeCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Set the target version as the new active
        await using var activateCmd = connection.CreateCommand();
        activateCmd.Transaction = tx;
        activateCmd.CommandText = """
            UPDATE dataset_versions
            SET status = 'Active',
                updated_at = $1,
                previous_active_version_id = $2
            WHERE version_id = $3;
            """;
        activateCmd.Parameters.AddWithValue(now);
        activateCmd.Parameters.AddWithValue((object?)currentActiveId ?? DBNull.Value);
        activateCmd.Parameters.AddWithValue(targetVersionId);
        await activateCmd.ExecuteNonQueryAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);

        return new RollbackResponse
        {
            PreviousActiveVersionId = currentActiveId ?? "(none)",
            NewActiveVersionId = targetVersionId
        };
    }

    public async Task<DatasetVersionRecord?> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT version_id, status, created_at, updated_at, source_path,
                   raw_artifact_path, processed_artifact_path, metadata,
                   previous_active_version_id, replaced_by_version_id, error_message
            FROM dataset_versions
            WHERE version_id = $1;
            """;
        cmd.Parameters.AddWithValue(versionId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
            return MapFromReader(reader);

        return null;
    }

    public async Task<DatasetVersionRecord?> GetActiveVersionAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT version_id, status, created_at, updated_at, source_path,
                   raw_artifact_path, processed_artifact_path, metadata,
                   previous_active_version_id, replaced_by_version_id, error_message
            FROM dataset_versions
            WHERE status = 'Active'
            LIMIT 1;
            """;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
            return MapFromReader(reader);

        return null;
    }

    public async Task<IReadOnlyList<DatasetVersionRecord>> ListVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT version_id, status, created_at, updated_at, source_path,
                   raw_artifact_path, processed_artifact_path, metadata,
                   previous_active_version_id, replaced_by_version_id, error_message
            FROM dataset_versions
            ORDER BY created_at DESC;
            """;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var versions = new List<DatasetVersionRecord>();
        while (await reader.ReadAsync(cancellationToken))
            versions.Add(MapFromReader(reader));

        return versions;
    }

    private static DatasetVersionRecord MapFromReader(NpgsqlDataReader reader)
    {
        return new DatasetVersionRecord
        {
            VersionId = reader.GetString(0),
            Status = Enum.Parse<VersionStatus>(reader.GetString(1)),
            CreatedAt = reader.GetDateTime(2),
            UpdatedAt = reader.GetDateTime(3),
            SourcePath = reader.IsDBNull(4) ? null : reader.GetString(4),
            RawArtifactPath = reader.IsDBNull(5) ? null : reader.GetString(5),
            ProcessedArtifactPath = reader.IsDBNull(6) ? null : reader.GetString(6),
            Metadata = reader.IsDBNull(7) ? null : reader.GetString(7),
            PreviousActiveVersionId = reader.IsDBNull(8) ? null : reader.GetString(8),
            ReplacedByVersionId = reader.IsDBNull(9) ? null : reader.GetString(9),
            ErrorMessage = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();
    }
}
