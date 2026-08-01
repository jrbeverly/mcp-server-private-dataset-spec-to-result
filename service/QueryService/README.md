# Query Service — Analytical Business Logic Core

**The product core. All analytical logic lives here and only here.**

No other component may contain analytical business logic. Adapters are transport-only; processing is batch-only; ingestion is validation-only.

## Responsibilities

- Resolve the active or requested dataset version
- Execute exploration and analytical queries against the serving store
- Retrieve raw evidence for verification and drill-down
- Shape outputs into compact structured responses per `contracts/ServiceContract/`
- Apply token-based access control (bearer token validation)
- Expose stable HTTP API contracts for adapters and evaluation harness

## Project Structure

```
service/QueryService/
├── src/
│   ├── QueryService.Api/         # HTTP API surface (Minimal API endpoints)
│   │   ├── Program.cs            # Host configuration, DI, middleware
│   │   └── Endpoints/            # One endpoint per file
│   ├── QueryService.Domain/      # Analytical business logic (THE product core)
│   │   ├── Services/             # Query execution, response shaping
│   │   └── Models/               # Domain models (distinct from contract types)
│   └── QueryService.Infrastructure/  # Data access, external dependencies
│       └── Repositories/         # Serving store access
```

## Hard Rule

**Analytical business logic belongs in `QueryService.Domain/` only.** If logic appears in `mcp/`, it must be extracted and moved here. The MCP adapter translates protocols — it does not filter, rank, aggregate, or enrich results.

## HTTP API

The direct HTTP API exposes the same five tool families defined in `contracts/ServiceContract/`:

| Family | HTTP Route | Purpose |
|--------|-----------|---------|
| Discovery | `POST /api/v1/discovery` | Find entities, cohorts, categories |
| Aggregation | `POST /api/v1/aggregation` | Counts, rankings, distributions |
| Metadata | `POST /api/v1/metadata` | Understand dataset dimensions |
| Drill-down | `POST /api/v1/drill-down` | Inspect a specific entity |
| Raw Evidence | `POST /api/v1/raw-evidence` | Underlying supporting records |

## Dependencies

- `contracts/ServiceContract/` — request/response types for the public API
- `contracts/ServiceConfiguration/` — corpus, storage, token configuration

## Anti-Patterns to Avoid

- ❌ Putting analytical logic in `mcp/McpAdapter/`
- ❌ Leaking storage column names into API responses
- ❌ Returning raw database rows without shaping
- ❌ Inline token validation in every endpoint (use middleware)

## Related

- [HLD.md: Query Service](../../../HLD.md#L195-L209)
- [contracts/ServiceContract/](../../../contracts/ServiceContract/)
