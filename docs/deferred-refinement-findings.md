# Deferred Refinement Findings

**Date:** 2026-05-18
**Context:** Benchmark-driven refinement Phase 4 identified 17 gaps. Seven were implemented in this cycle. The remaining ten are deferred below with rationale.

## P2 — Next Priority (4 findings)

### CQ-2: No property-based search in discovery

**Finding:** `SearchEntities` SQL only targets `name` and `description` columns. The `properties` JSONB column has a GIN index but is unused in any query. An LLM cannot ask "find entities where headquarters is in California."

**Why deferred:** This requires contract changes to `DiscoveryRequest` (adding a `PropertyFilter` parameter), SQL changes to integrate GIN index queries, and service changes to route property filters. Estimated 3 days. Defer to next refinement cycle when metadata exploration data confirms which properties actually exist.

**Insertion point:** After `CQ-1` (evidence pipeline) is validated with real data. Insert into the spaced issue tree as a new `discovery-property-search` issue under the refinement parent.

### CQ-3: Single-dimension aggregation (only "category")

**Finding:** `AggregateService` hardcodes `groupBy` to "category" only. Property-based grouping (e.g., "by region", "by industry") is unsupported.

**Why deferred:** Requires new SQL aggregation queries that dynamically `GROUP BY` property keys, a routing layer in `AggregateService` to select the appropriate SQL, and contract changes to advertise available dimensions. Estimated 3 days. The metadata exploration improvement (TS-1) is a prerequisite since it surfaces available property keys.

**Insertion point:** After available properties are verified in metadata exploration. Insert as `aggregate-property-dimensions` under the refinement parent.

### CQ-4: Hardcoded `RelationshipType = "category"`

**Finding:** `DrillDownService.MapToRelated()` always sets `RelationshipType = "category"`. There is no mechanism for typed or weighted relationships.

**Why deferred:** Requires either a relationships schema (new table or JSONB field) or inference logic from shared properties. Estimated 2.5 days. Depends on data model decisions about how relationships are stored.

**Insertion point:** After enrichment improvements provide relationship data. Insert as `entity-relationships` under the refinement parent.

### CQ-5: No validated metric set (silent fallback to percentage)

**Finding:** When `aggregate` receives an unrecognized metric name, `MapToBucket` silently falls back to category percentage rather than reporting the metric was unknown.

**Why deferred:** A trivial fix (throw `BadRequestException` for unknown metrics) would be harsh if the LLM is guessing metric names. The better fix depends on TS-1 (available metrics now surfaced in metadata exploration) — once the LLM can discover valid metrics, we can validate more strictly. Estimated 2 days.

**Insertion point:** After TS-1 validation. Insert as `aggregate-metric-validation` under the refinement parent.

## P3 — Deferred Long-Term (6 findings)

### CF-1: Connection pooling optimization

**Finding:** `PostgresServingStoreRepository` opens a new connection per method call via `DataSource.OpenConnectionAsync()`.

**Why deferred:** Npgsql's `NpgsqlDataSource` already manages a connection pool internally (`Pooling=true` by default, `MaxPoolSize=100`). The `OpenConnectionAsync()` call leases from the pool; it does not create a new TCP connection. Additional optimization would be marginal. Not worth implementation complexity for v1.

### CF-2: Bulk load uses row-by-row inserts

**Finding:** `PostgresServingStoreRepository.LoadVersionAsync()` inserts one entity/evidence record at a time via `ExecuteNonQueryAsync` in a loop.

**Why deferred:** Version loading is an infrequent background operation performed once per dataset publication. It does not affect online query latency. Switching to `NpgsqlBinaryImporter` or multi-row `INSERT` would reduce publication latency but adds complexity to an offline path. Not worth the maintenance burden for v1.

### CF-3: No cross-version comparison tool

**Finding:** No MCP tool or API endpoint compares entities, aggregations, or evidence across two dataset versions.

**Why deferred:** This is a new tool family requiring new contract types, a comparison service, and MCP tool implementation. It is a feature addition, not a refinement. Should be scoped as a separate issue.

### CF-4: Weak enrichment (only 3 derived fields)

**Finding:** `EnrichmentStep` adds only `total` metric, `property_count`, and `_entity_id`. No classification, tagging, outlier detection, or cross-entity inference.

**Why deferred:** Enrichment quality depends heavily on the specific corpus domain. What constitutes useful enrichment for competitive intelligence datasets may differ from other domains. Defer until corpus-specific enrichment needs are proven through answer quality evaluation.

### CF-5: No explicit relationship model

**Finding:** `CanonicalEntity` has no relationship fields. Related entities are inferred solely from shared category membership.

**Why deferred:** Requires data model changes to the serving store schema (`serving_relationships` table or JSONB relationship fields), changes to the processing pipeline to produce relationship data, and changes to the query service to expose relationships. This is a schema-level change affecting ingestion, processing, and serving layers. Should be designed as a v2 feature.

### CF-6: No type system for properties and metrics

**Finding:** Properties are `Dictionary<string, string>` and metrics are `Dictionary<string, decimal>`. No units, data types, valid ranges, or descriptions.

**Why deferred:** Requires a metadata registry (schema-on-read for property types, metric unit declarations), integration into the processing pipeline for metadata generation, and contract changes to expose type information. Significant design work needed. Defer to v2.

### CF-7: Categories are opaque strings

**Finding:** Category values are free-form strings with no descriptions, hierarchy, or taxonomy.

**Why deferred:** Requires category taxonomy design (hierarchy model, descriptions, parent-child relationships), schema changes to the serving store, and integration into metadata exploration. Should be designed alongside the relationship model (CF-5) and type system (CF-6) as part of a v2 schema upgrade.

### RS-2: Flat JSON responses (no structured MCP content)

**Finding:** All MCP tool responses are flat JSON strings serialized via `McpToolResult.Format()`. There is no use of MCP structured content types.

**Why deferred:** The MCP protocol primarily uses `text` content type for tool results. Structured content types (images, embedded tables, etc.) are appropriate for rich media but add complexity without clear ROI for text-based analytical workflows. Revisit if LLM consumption patterns show benefit.
