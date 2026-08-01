# Refinement Triage — Benchmark-Driven Findings

**Date:** 2026-05-18
**Phase:** 4 (Evaluation and Refinement per HLD)

## Triage Framework

Findings are categorized into four buckets matching the HLD's risk taxonomy:

| Category | Scope | Example |
|---|---|---|
| **Tool-Surface** | MCP tool schemas, input/output contracts, API contracts | Missing fields, unclear parameters, discarded data |
| **Corpus-Quality** | Processing pipeline, data model, enrichment, serving store | Missing evidence, weak enrichment, single-dimension aggregation |
| **Response-Shaping** | Output format, structure, content, LLM-friendliness | Flat JSON, missing metadata, opaque identifiers |
| **Connector/Workflow Friction** | Performance, adapters, deployment, cross-version tooling | Extra DB round trips, row-by-row bulk load, missing comparison tools |

Each finding is scored on Impact (1–5, how directly it degrades LLM answer quality) and Effort (1–5, person-days to implement). Priority = Impact / Effort.

## Prioritized Refinement Backlog

### Implemented (this cycle)

| ID | Finding | Category | Impact | Effort | Change Summary |
|----|---------|----------|--------|--------|----------------|
| TS-1 | Metadata exploration hid available metrics/properties | Tool-Surface | 5 | 1 | Added `AvailableMetrics`, `AvailableProperties` to `MetadataExplorationResponse`; service now calls existing repository methods |
| TS-2 | Discovery discarded relevance scores | Tool-Surface | 3.5 | 1 | Added `similarity()` to `SearchEntities` SELECT; `RelevanceScore` now populated on `DiscoveredEntity` |
| TS-3 | Discovery hid entity descriptions | Tool-Surface | 3.5 | 1 | Added `Description` field to `DiscoveredEntity` |
| TS-4 | Aggregate only exposed Avg — hid Min/Max/Sum | Tool-Surface | 4 | 1 | Added `Min`, `Max`, `Sum` to `AggregateBucket`; fixed case-insensitive JSON deserialization |
| TS-5 | `groupBy` constraint not advertised | Tool-Surface | 2.5 | 1 | Changed `groupBy` parameter to JSON Schema enum `["category"]` |
| CQ-1 | No evidence pipeline — `evidence.json` never produced | Corpus-Quality | 4.5 | 2 | Created `EvidenceStep` pipeline step; generates properties and metrics evidence records from enriched entities |
| RS-1 | Redundant entity existence check in RawEvidence | Response-Shaping | 1.5 | 1 | Removed extra `GetEntityByIdAsync` call; nonexistent entity now returns empty results instead of throwing |

### P2 — Next Priority

| ID | Finding | Category | Impact | Effort | Notes |
|----|---------|----------|--------|--------|-------|
| CQ-2 | No property-based search in discovery | Corpus-Quality | 3 | 3 | Requires `DiscoveryRequest` update, GIN index integration on `properties` JSONB |
| CQ-3 | Single-dimension aggregation (only "category") | Corpus-Quality | 2.5 | 3 | Requires new SQL for property-based `GROUP BY`, dynamic dimension routing in `AggregateService` |
| CQ-4 | Hardcoded `RelationshipType = "category"` | Corpus-Quality | 2 | 2.5 | Requires relationship model in schemas or inference from shared properties |
| CQ-5 | No validated metric set (silent fallback) | Corpus-Quality | 2 | 2 | Could improve error messages or validate against available metrics list |

### P3 — Deferred

| ID | Finding | Category | Notes |
|----|---------|--------|-------|
| CF-1 | No connection pooling optimization | Connector Friction | Npgsql pools by default; `NpgsqlDataSource` is already a pool |
| CF-2 | Bulk load uses row-by-row inserts | Connector Friction | Batch operation, not online path; infrequent |
| CF-3 | No cross-version comparison tool | Connector Friction | Requires new tool family; out of scope for v1 |
| CF-4 | Weak enrichment (only 3 derived fields) | Corpus-Quality | Needs corpus-specific enrichment design; defer to next dataset |
| CF-5 | No explicit relationship model | Corpus-Quality | Requires schema changes; defer to v2 |
| CF-6 | No type system for properties/metrics | Corpus-Quality | Requires metadata registry; defer to v2 |
| CF-7 | Categories are opaque strings | Corpus-Quality | Requires taxonomy design; defer to v2 |
| RS-2 | Flat JSON responses (no structured MCP content) | Response-Shaping | MCP protocol is text-native; structured types add complexity for v1 |

## Measurable Improvement Evidence

### Before (baseline state)
- `explore_metadata` returned dimensions and counts only — no way to discover metric/property keys
- `aggregate` returned only `avg` per bucket — min/max/sum were computed in SQL but discarded
- `discover` returned entity IDs and names without descriptions or relevance ranking signals
- `get_evidence` always returned empty results (no evidence pipeline existed)
- Aggregate `groupBy` was free-text, causing runtime errors for unsupported values

### After (this cycle)
- `explore_metadata` returns `availableMetrics` and `availableProperties` arrays
- `aggregate` returns `min`, `max`, `sum` alongside `value` when metric summaries are available
- `discover` returns `description` and `relevanceScore` for each entity
- Processing pipeline generates `evidence.json` with properties and metrics evidence per entity
- `groupBy` is schema-enforced to `"category"` only

### Verification
- 126 tests pass across all 5 test projects (added 11 new tests)
- All contract changes are backward-compatible (new fields are optional `?`)
- Build: 0 warnings, 0 errors
