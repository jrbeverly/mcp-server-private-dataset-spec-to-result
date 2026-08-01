namespace QueryService.Infrastructure;

public static class ServingStoreSchema
{
    public const string EnsureSchema = """
        CREATE EXTENSION IF NOT EXISTS pg_trgm;

        -- Analytical surface: the primary serving table for discovery,
        -- drill-down, aggregation, and metadata exploration queries.
        CREATE TABLE IF NOT EXISTS serving_entities (
            entity_id   VARCHAR(256) NOT NULL,
            name        VARCHAR(1024) NOT NULL,
            category    VARCHAR(256),
            description TEXT,
            properties  JSONB NOT NULL DEFAULT '{}',
            metrics     JSONB NOT NULL DEFAULT '{}',
            version_id  VARCHAR(64) NOT NULL,
            PRIMARY KEY (version_id, entity_id)
        );

        -- Raw evidence surface: supporting records linked to entities.
        -- Kept separate from the analytical surface so evidence retrieval
        -- never competes with discovery/aggregation for index usage.
        CREATE TABLE IF NOT EXISTS serving_evidence (
            record_id        VARCHAR(256) NOT NULL,
            entity_id        VARCHAR(256) NOT NULL,
            type             VARCHAR(128) NOT NULL,
            fields           JSONB NOT NULL DEFAULT '{}',
            source_timestamp TIMESTAMPTZ,
            version_id       VARCHAR(64) NOT NULL,
            PRIMARY KEY (version_id, record_id)
        );

        -- Tracks which versions have been materialised and which is active.
        -- At most one row has is_active = TRUE at any time.
        CREATE TABLE IF NOT EXISTS serving_version_state (
            version_id     VARCHAR(64) PRIMARY KEY,
            loaded_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            entity_count   INT NOT NULL DEFAULT 0,
            evidence_count INT NOT NULL DEFAULT 0,
            is_active      BOOLEAN NOT NULL DEFAULT FALSE
        );

        -- Indexes for the analytical surface
        CREATE INDEX IF NOT EXISTS idx_se_version_category
            ON serving_entities (version_id, category);
        CREATE INDEX IF NOT EXISTS idx_se_name_trgm
            ON serving_entities USING gin (name gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS idx_se_properties
            ON serving_entities USING gin (properties jsonb_path_ops);
        CREATE INDEX IF NOT EXISTS idx_se_metrics
            ON serving_entities USING gin (metrics jsonb_path_ops);

        -- Indexes for the raw evidence surface
        CREATE INDEX IF NOT EXISTS idx_ev_version_entity
            ON serving_evidence (version_id, entity_id);
        CREATE INDEX IF NOT EXISTS idx_ev_version_type
            ON serving_evidence (version_id, type);

        -- Partial index so is_active lookups hit a tiny index
        CREATE INDEX IF NOT EXISTS idx_svs_active
            ON serving_version_state (version_id)
            WHERE is_active = TRUE;
        """;

    public const string ClearVersionData = """
        DELETE FROM serving_entities  WHERE version_id = @versionId;
        DELETE FROM serving_evidence  WHERE version_id = @versionId;
        DELETE FROM serving_version_state WHERE version_id = @versionId;
        """;

    public const string ActivateVersion = """
        UPDATE serving_version_state SET is_active = FALSE WHERE is_active = TRUE;
        UPDATE serving_version_state SET is_active = TRUE  WHERE version_id = @versionId;
        """;

    public const string DeactivateVersion = """
        UPDATE serving_version_state SET is_active = FALSE WHERE version_id = @versionId;
        """;

    public const string GetActiveVersionId = """
        SELECT version_id FROM serving_version_state WHERE is_active = TRUE LIMIT 1;
        """;

    public const string InsertEntity = """
        INSERT INTO serving_entities
            (entity_id, name, category, description, properties, metrics, version_id)
        VALUES
            (@entityId, @name, @category, @description, @properties::jsonb, @metrics::jsonb, @versionId);
        """;

    public const string InsertEvidence = """
        INSERT INTO serving_evidence
            (record_id, entity_id, type, fields, source_timestamp, version_id)
        VALUES
            (@recordId, @entityId, @type, @fields::jsonb, @sourceTimestamp, @versionId);
        """;

    public const string UpsertVersionState = """
        INSERT INTO serving_version_state (version_id, loaded_at, entity_count, evidence_count, is_active)
        VALUES (@versionId, NOW(), @entityCount, @evidenceCount, FALSE)
        ON CONFLICT (version_id) DO UPDATE SET
            loaded_at = NOW(),
            entity_count = @entityCount,
            evidence_count = @evidenceCount;
        """;

    // Query templates for the five tool families

    public const string SearchEntities = """
        SELECT entity_id, name, category, description, properties, metrics,
               CASE WHEN @query IS NOT NULL
                    THEN similarity(name, @query)
                    ELSE 0 END AS relevance_score
        FROM serving_entities
        WHERE version_id = @versionId
          AND (@query IS NULL
               OR name ILIKE '%' || @query || '%'
               OR description ILIKE '%' || @query || '%')
          AND (@category IS NULL OR category = @category)
        ORDER BY
          CASE WHEN @query IS NOT NULL
               THEN similarity(name, @query)
               ELSE 0 END DESC,
          name ASC
        OFFSET @offset LIMIT @limit;
        """;

    public const string CountSearchEntities = """
        SELECT COUNT(*)
        FROM serving_entities
        WHERE version_id = @versionId
          AND (@query IS NULL
               OR name ILIKE '%' || @query || '%'
               OR description ILIKE '%' || @query || '%')
          AND (@category IS NULL OR category = @category);
        """;

    public const string GetEntityById = """
        SELECT entity_id, name, category, description, properties, metrics
        FROM serving_entities
        WHERE version_id = @versionId AND entity_id = @entityId;
        """;

    public const string GetEntitiesByCategory = """
        SELECT entity_id, name, category, description, properties, metrics
        FROM serving_entities
        WHERE version_id = @versionId AND category = @category
        ORDER BY name ASC;
        """;

    public const string GetCategoryAggregates = """
        SELECT
            category AS key,
            ''    AS label,
            COUNT(*)::int AS count,
            ROUND(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER(), 2) AS percentage,
            jsonb_object_agg(metric_key, metric_summary) AS metric_summaries
        FROM (
            SELECT
                category,
                m.key AS metric_key,
                jsonb_build_object(
                    'min', MIN((m.value::text)::numeric),
                    'max', MAX((m.value::text)::numeric),
                    'avg', ROUND(AVG((m.value::text)::numeric), 2),
                    'sum', SUM((m.value::text)::numeric)
                ) AS metric_summary
            FROM serving_entities,
                 jsonb_each(metrics) AS m(key, value)
            WHERE version_id = @versionId AND category IS NOT NULL
            GROUP BY category, m.key
        ) sub
        GROUP BY category
        ORDER BY count DESC;
        """;

    public const string GetTotalEntityCount = """
        SELECT COUNT(*) FROM serving_entities WHERE version_id = @versionId;
        """;

    public const string GetTotalCategoryCount = """
        SELECT COUNT(DISTINCT category)
        FROM serving_entities
        WHERE version_id = @versionId AND category IS NOT NULL;
        """;

    public const string GetDimensionValues = """
        SELECT DISTINCT
            category AS value,
            category AS label,
            COUNT(*)::int AS entity_count
        FROM serving_entities
        WHERE version_id = @versionId AND category IS NOT NULL
        GROUP BY category
        ORDER BY entity_count DESC;
        """;

    public const string GetAvailableMetrics = """
        SELECT DISTINCT m.key
        FROM serving_entities, jsonb_each(metrics) AS m(key, value)
        WHERE version_id = @versionId
        ORDER BY m.key;
        """;

    public const string GetAvailableProperties = """
        SELECT DISTINCT p.key
        FROM serving_entities, jsonb_each(properties) AS p(key, value)
        WHERE version_id = @versionId
        ORDER BY p.key;
        """;

    public const string GetEvidenceByEntity = """
        SELECT record_id, entity_id, type, fields, source_timestamp
        FROM serving_evidence
        WHERE version_id = @versionId AND entity_id = @entityId
          AND (@type IS NULL OR type = @type)
        ORDER BY source_timestamp DESC NULLS LAST
        OFFSET @offset LIMIT @limit;
        """;

    public const string CountEvidenceByEntity = """
        SELECT COUNT(*)
        FROM serving_evidence
        WHERE version_id = @versionId AND entity_id = @entityId
          AND (@type IS NULL OR type = @type);
        """;

    public const string GetRelatedEntities = """
        SELECT e.entity_id, e.name, e.category
        FROM serving_entities e
        INNER JOIN serving_entities target
            ON target.version_id = e.version_id
            AND target.entity_id = @entityId
            AND target.category IS NOT NULL
            AND e.category = target.category
            AND e.entity_id != target.entity_id
        WHERE e.version_id = @versionId
        ORDER BY
            (SELECT SUM((m.value::text)::numeric)
             FROM jsonb_each(e.metrics) AS m(key, value)) DESC NULLS LAST
        LIMIT 10;
        """;

    public const string GetAllEntitiesByVersion = """
        SELECT entity_id, name, category, description, properties, metrics
        FROM serving_entities
        WHERE version_id = @versionId
        ORDER BY entity_id;
        """;

    public const string GetAllEvidenceByVersion = """
        SELECT record_id, entity_id, type, fields, source_timestamp
        FROM serving_evidence
        WHERE version_id = @versionId
        ORDER BY record_id;
        """;
}
