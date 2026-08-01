# Serving Store Schema and Published Analytical Views

## Purpose

Define the PostgreSQL schema that materializes published dataset versions for online analytical querying, and the version loading mechanism that bridges the offline processing pipeline to the online serving store.

## Context

The system has an offline processing pipeline (ingestion → processing → publication) that produces structured JSON artifacts per dataset version. The online query service needs those artifacts materialized into indexed tables so it can execute discovery, aggregation, metadata exploration, drill-down, and raw evidence retrieval queries with low latency.

The HLD is explicit: the serving store must be shaped around analytical access patterns, not simple document retrieval. It must also keep the analytical surface and raw evidence surface distinct.

## Approach

### Two-Surface Design

The serving store models two surfaces as physically separate tables over the same published corpus:

| Surface | Table | Purpose |
|---|---|---|
| Analytical | `serving_entities` | Discovery, aggregation, metadata, drill-down |
| Raw evidence | `serving_evidence` | Supporting records for verification |

This separation prevents evidence-heavy queries from competing with analytical index usage, and keeps the two access patterns independently optimizable.

### Version Lifecycle

```
Processing pipeline          Serving store
─────────────────          ─────────────
Raw → Processing           (no serving tables yet)
Processed → Published  ──→ LoadVersion (INSERT into serving_*)
                       ──→ ActivateVersion (is_active = TRUE)
                       ←── Rollback (activate a different loaded version)
```

Each published version is loaded into the serving tables independently. Only one version is active at a time. Rollback to a previously loaded version is a metadata-only operation (no data copy needed).

### Loading is Separate from Compilation

The `ServingVersionLoader` reads processed JSON artifacts from disk and bulk-inserts them into the serving tables. It is invoked after the processing pipeline publishes a version. This keeps the processing pipeline (compute-heavy, DuckDB-based) operationally decoupled from the serving store (lightweight, PostgreSQL-only).

## Schema

### `serving_entities` — Analytical Surface

```sql
CREATE TABLE serving_entities (
    entity_id   VARCHAR(256) NOT NULL,
    name        VARCHAR(1024) NOT NULL,
    category    VARCHAR(256),
    description TEXT,
    properties  JSONB NOT NULL DEFAULT '{}',
    metrics     JSONB NOT NULL DEFAULT '{}',
    version_id  VARCHAR(64) NOT NULL,
    PRIMARY KEY (version_id, entity_id)
);
```

One row per canonical entity per version. `properties` and `metrics` use JSONB so the schema stays stable as datasets add new dimensions.

### `serving_evidence` — Raw Evidence Surface

```sql
CREATE TABLE serving_evidence (
    record_id        VARCHAR(256) NOT NULL,
    entity_id        VARCHAR(256) NOT NULL,
    type             VARCHAR(128) NOT NULL,
    fields           JSONB NOT NULL DEFAULT '{}',
    source_timestamp TIMESTAMPTZ,
    version_id       VARCHAR(64) NOT NULL,
    PRIMARY KEY (version_id, record_id)
);
```

One row per evidence record per version. Linked to the analytical surface via `entity_id`.

### `serving_version_state` — Activation Tracking

```sql
CREATE TABLE serving_version_state (
    version_id     VARCHAR(64) PRIMARY KEY,
    loaded_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    entity_count   INT NOT NULL DEFAULT 0,
    evidence_count INT NOT NULL DEFAULT 0,
    is_active      BOOLEAN NOT NULL DEFAULT FALSE
);
```

At most one row has `is_active = TRUE`. A partial index on `WHERE is_active = TRUE` keeps the active-version lookup fast.

## Indexing Strategy

Indexes are chosen for the planned query patterns, not for storage internals:

| Index | Query pattern served |
|---|---|
| `PRIMARY KEY (version_id, entity_id)` | Drill-down by entity ID within a version |
| `idx_se_version_category (version_id, category)` | Category-filtered discovery and aggregates |
| `idx_se_name_trgm GIN (name gin_trgm_ops)` | Free-text entity name search with similarity ranking |
| `idx_se_properties GIN (properties jsonb_path_ops)` | Property key existence checks |
| `idx_se_metrics GIN (metrics jsonb_path_ops)` | Metric key existence checks |
| `PRIMARY KEY (version_id, record_id)` | Evidence lookup by record ID |
| `idx_ev_version_entity (version_id, entity_id)` | Evidence retrieval for a specific entity |
| `idx_ev_version_type (version_id, type)` | Evidence filtered by type |
| `idx_svs_active WHERE is_active = TRUE` | Active version resolution (single-row index) |

The trigram index on `name` supports `ILIKE '%pattern%'` searches with `similarity()` ordering, which is the primary discovery query path.

## Query Pattern Mapping

### Discovery (`GET /api/v1/discovery`)

```sql
SELECT * FROM serving_entities
WHERE version_id = $v
  AND (name ILIKE '%' || $q || '%' OR description ILIKE '%' || $q || '%')
  AND ($cat IS NULL OR category = $cat)
ORDER BY similarity(name, $q) DESC, name ASC
OFFSET $o LIMIT $l;
```

Uses trigram index for text search, version+category index for filtering.

### Aggregation (`GET /api/v1/aggregation`)

```sql
SELECT category, COUNT(*),
       jsonb_object_agg(metric_key, jsonb_build_object('min', MIN, 'max', MAX, 'avg', AVG, 'sum', SUM))
FROM serving_entities, jsonb_each(metrics)
WHERE version_id = $v AND category IS NOT NULL
GROUP BY category, metric_key;
```

Uses version+category index, then expands JSONB metrics in memory.

### Metadata Exploration (`GET /api/v1/metadata`)

```sql
SELECT DISTINCT category, COUNT(*) FROM serving_entities
WHERE version_id = $v AND category IS NOT NULL
GROUP BY category ORDER BY COUNT(*) DESC;
```

Uses version+category index. Available metrics/properties use `jsonb_each()` with `DISTINCT`.

### Drill-Down (`GET /api/v1/drill-down`)

```sql
-- Entity detail
SELECT * FROM serving_entities WHERE version_id = $v AND entity_id = $e;

-- Related entities (same category, ordered by total metric value)
SELECT e.* FROM serving_entities e
INNER JOIN serving_entities target
  ON target.version_id = e.version_id AND target.entity_id = $e
  AND target.category IS NOT NULL AND e.category = target.category
  AND e.entity_id != target.entity_id
WHERE e.version_id = $v
ORDER BY (SELECT SUM(...) FROM jsonb_each(e.metrics)) DESC NULLS LAST
LIMIT 10;
```

Uses primary key for direct lookup, version+category index for related entities.

### Raw Evidence (`GET /api/v1/raw-evidence`)

```sql
SELECT * FROM serving_evidence
WHERE version_id = $v AND entity_id = $e
  AND ($type IS NULL OR type = $type)
ORDER BY source_timestamp DESC NULLS LAST
OFFSET $o LIMIT $l;
```

Uses version+entity index and version+type index.

## Decisions

- **Decision:** Use JSONB for properties and metrics rather than EAV or separate tables.
  **Rationale:** The schema must stay stable across datasets with varying attribute sets. JSONB avoids schema migrations when new metrics are added, supports GIN indexing for key existence checks, and allows `jsonb_each()` for dimension discovery.

- **Decision:** Store entities and evidence in the same PostgreSQL database but separate tables.
  **Rationale:** Simpler operations (one database to manage), but physical separation prevents evidence queries from competing with analytical index usage.

- **Decision:** Index name with `pg_trgm` GIN rather than a full-text search vector.
  **Rationale:** The search workload is entity name and description lookup, not document search. Trigrams handle substring matching well, and `similarity()` provides a natural relevance score for discovery results.

- **Decision:** Keep version loading as an explicit, separate operation from processing.
  **Rationale:** This allows re-loading a version without re-processing, refreshing schemas without re-running the pipeline, and rollback to previously loaded versions.

## Constraints

- Version data is co-located in shared tables (partitioned by `version_id`). Separate tables per version were considered but rejected: they complicate query parameterization across versions and bloat the catalog.
- The `pg_trgm` extension must be enabled in the PostgreSQL database. The schema init includes `CREATE EXTENSION IF NOT EXISTS`.
- Version loading is a bulk-insert operation. For very large datasets (>1M entities), consider switching to Npgsql binary COPY instead of batched INSERT.
- Evidence data is optional. If no `evidence.json` exists in the processed artifact directory, the evidence surface remains empty.

## Evolution

- If per-version table size becomes a problem, add range partitioning on `version_id`.
- If aggregated queries are a latency bottleneck, add materialized views for common aggregate patterns.
- If multi-corpus deployments are introduced, add a `corpus_id` column to all serving tables.
