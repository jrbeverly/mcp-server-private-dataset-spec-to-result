# Processing — Dataset Enrichment and Publication

**Transforms raw ingested snapshots into optimized analytical serving views.**

## Responsibilities

- Normalize source records into canonical structures
- Derive comparative and aggregate metrics
- Build category and ecosystem views
- Generate metadata that improves exploration
- Prepare raw evidence views for later inspection
- Publish processed outputs for a specific dataset version

## Processing Pipeline

1. **Load** — Read raw snapshot from versioned object storage
2. **Normalize** — Canonicalize records into standard structures (DuckDB batch compilation)
3. **Enrich** — Derive aggregate metrics, build category views, generate exploration metadata
4. **Validate** — Assert structural integrity of processed outputs
5. **Publish** — Write processed views to the serving store, register version as active via `service/`

## What this component does NOT do

- Raw ingestion (belongs to `ingestion/`)
- Online query serving (belongs to `service/`)
- MCP protocol handling (belongs to `mcp/`)

## Entry Points

- Batch processing jobs (containerized, manually triggered or scheduled)
- Publication workflow that activates a new dataset version

## Dependencies

- `contracts/ServiceContract/` — for version and response types
- `contracts/ServiceConfiguration/` — for storage backend configuration
- `ingestion/IngestionService/` — source of raw dataset snapshots
