# Developer Bootstrap

**Local development setup and boundary conventions.**

## Prerequisites

- .NET SDK 8.0+
- Docker (for PostgreSQL in local development)
- Git

## One-Command Bootstrap

```bash
make setup        # Restore NuGet packages
make build        # Compile all projects
```

## Verifying the Build

```bash
dotnet build                          # Build everything
dotnet test                           # Run tests (when implemented)
```

## Component Overview

A local developer workflow from source to running service:

```
contracts/                    ← Shared types only (no business logic)
  ServiceContract/            ← Versioned request/response types
  ServiceConfiguration/       ← Environment-based configuration

service/QueryService/         ← The product core
  QueryService.Api/           ← HTTP API (Minimal API endpoints)
  QueryService.Domain/        ← ALL analytical business logic
  QueryService.Infrastructure/ ← Data access (repositories)

mcp/McpAdapter/               ← Thin ChatGPT transport adapter
  Calls service via HTTP — no direct DB, no domain logic

ingestion/                     ← Raw dataset import (batch)
processing/                    ← Enrichment and publication (batch)
evaluation/                    ← Answer quality measurement
ops/                           ← Deployment and scripts
```

## Dependency Direction

```
contracts/ (shared types)       ← Bottom layer, no dependencies on components
    ↑
service/QueryService/           ← Implements against contracts
    ↑  (via HTTP only — no project reference)
mcp/McpAdapter/                 ← Calls service, uses contract types

processing/ → depends on contracts/ only
ingestion/  → depends on contracts/ only
evaluation/ → depends on contracts/ and service/ (via HTTP)
```

**Hard rule:** `mcp/McpAdapter/` does NOT reference `service/QueryService.Domain/`. All analytical logic flows through the HTTP API.

## Running the Query Service Locally

```bash
# Set required environment variables
export CORPUS_DEPLOYMENT_NAME=local-dev
export CORPUS_ID=local-corpus
export AUTH_TOKEN=dev-token
export STORAGE_POSTGRES_CONNECTION_STRING="Host=localhost;Database=corpus;Username=postgres;Password=postgres"
export STORAGE_S3_BUCKET_NAME=local-artifacts

# Run the service
cd service/QueryService/src/QueryService.Api
dotnet run

# Service listens on http://localhost:5000
```

## Testing Endpoints

```bash
# Discovery
curl -X POST http://localhost:5000/api/v1/discovery \
  -H "X-Api-Token: dev-token" \
  -H "Content-Type: application/json" \
  -d '{"query": "example", "limit": 10}'

# Aggregation
curl -X POST http://localhost:5000/api/v1/aggregation \
  -H "X-Api-Token: dev-token" \
  -H "Content-Type: application/json" \
  -d '{"metric": "count", "groupBy": "category"}'

# Metadata exploration
curl -X POST http://localhost:5000/api/v1/metadata \
  -H "X-Api-Token: dev-token" \
  -H "Content-Type: application/json" \
  -d '{"category": "competitors"}'

# Drill-down
curl -X POST http://localhost:5000/api/v1/drill-down \
  -H "X-Api-Token: dev-token" \
  -H "Content-Type: application/json" \
  -d '{"entityId": "entity_001"}'

# Raw evidence
curl -X POST http://localhost:5000/api/v1/raw-evidence \
  -H "X-Api-Token: dev-token" \
  -H "Content-Type: application/json" \
  -d '{"entityId": "entity_001"}'
```

## Where to Place Code

When adding new functionality, follow these rules:

| What you're building | Where it goes | Example |
|---------------------|---------------|---------|
| A new analytical query | `service/QueryService.Domain/Services/` | `ITrendService` |
| A new API endpoint | `service/QueryService.Api/Endpoints/` | `Trends/TrendEndpoint.cs` |
| A new request/response type | `contracts/ServiceContract/src/V1/` | `Trending.cs` |
| A new MCP tool | `mcp/McpAdapter/src/Tools/` | `TrendTool.cs` (thin wrapper) |
| A new configuration section | `contracts/ServiceConfiguration/src/` | `RateLimitConfig.cs` |
| A new processing job | `processing/ProcessingService/src/` | `TrendCompiler.cs` |
| An ingestion validator | `ingestion/IngestionService/src/` | `SchemaValidator.cs` |

**If you're writing logic that filters, ranks, aggregates, or enriches data — it belongs in `service/QueryService.Domain/`. Never in `mcp/`.**

## Environment Variables Reference

### Required

| Variable | Purpose |
|----------|---------|
| `CORPUS_DEPLOYMENT_NAME` | Unique name for this deployment |
| `CORPUS_ID` | Corpus identifier |
| `AUTH_TOKEN` | Static bearer token for API access |
| `STORAGE_POSTGRES_CONNECTION_STRING` | PostgreSQL connection string |
| `STORAGE_S3_BUCKET_NAME` | S3 bucket for raw/processed artifacts |

### Optional

| Variable | Default | Purpose |
|----------|---------|---------|
| `AUTH_HEADER_NAME` | `X-Api-Token` | Header to check for auth token |
| `STORAGE_POSTGRES_SCHEMA` | `public` | PostgreSQL schema |
| `STORAGE_S3_REGION` | — | S3 region override |
| `STORAGE_S3_RAW_PREFIX` | `raw/` | S3 prefix for raw snapshots |
| `STORAGE_S3_PROCESSED_PREFIX` | `processed/` | S3 prefix for processed views |
| `DATASET_ACTIVE_VERSION` | — | Pinned active version (otherwise resolved from store) |
| `DATASET_DEFAULT_VERSION` | — | Fallback version |
| `DATASET_ALLOW_VERSION_PINNING` | `true` | Allow requests to pin to a specific version |
| `CORPUS_DESCRIPTION` | — | Human-readable corpus description |

## Adding a New Tool Family (e.g., V2 Contract)

1. Create `contracts/ServiceContract/src/V2/` with new request/response types
2. Add domain service interface in `service/QueryService.Domain/Services/`
3. Add endpoint in `service/QueryService.Api/Endpoints/`
4. Add thin MCP tool in `mcp/McpAdapter/src/Tools/`
5. Do NOT modify V1 types in place — they remain supported until all callers migrate

## Related Documents

- [CODEMAP.md](../CODEMAP.md) — Repository structure and conventions
- [HLD.md](../HLD.md) — Architecture decisions and component design
- [.claude/CLAUDE.md](../.claude/CLAUDE.md) — AI assistant context
- [contracts/ServiceContract/README.md](../contracts/ServiceContract/README.md) — Contract design rules
- [service/QueryService/README.md](../service/QueryService/README.md) — Domain layer rules
