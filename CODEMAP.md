# CODEMAP - mcp-server-paid-dataset

**Purpose:** Repository-specific structure, technology stack, and implementation patterns.

---

## Repository Overview

This repository implements a **hosted AI-accessible intelligence service** that provides large language models with access to curated, structured, domain-specific commercial datasets through an MCP-compatible integration layer.

**One deployment per intelligence corpus.** The same system shape serves one shared global corpus; additional corpora mean additional deployments, not multi-tenancy.

---

## Top-Level Structure

```
contracts/          # Shared internal contract — the only coupling surface between components
ingestion/          # Raw dataset import, validation, and version registration
processing/         # Normalization, enrichment, and publication jobs
service/            # Query service — analytical business logic core
mcp/                # ChatGPT-facing MCP adapter (thin transport layer)
evaluation/         # Benchmark prompts, scoring, quality comparisons
ops/                # Deployment and publication scripts
docs/               # Architecture, decisions, and developer guides
```

### Dependency Direction

```
contracts/ServiceContract/       (shared models — no dependencies on other components)
    ↑ depends on
ingestion/  processing/  service/  mcp/  evaluation/
    ↑
ops/  (orchestrates everything)
```

- `service/` and `mcp/` depend on `contracts/ServiceContract/` — never the reverse
- `service/` never depends on `mcp/` — the core must remain adapter-agnostic
- Business logic lives only in `service/` — adapters are transport-only

---

## Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Backend | C# .NET | 8.0 LTS |
| API pattern | ASP.NET Minimal API | 8.0 |
| Serving store | PostgreSQL | 16 |
| Batch processing | DuckDB | latest |
| Artifact storage | S3 | — |
| IaC | Terraform | 1.6+ |
| Auth (v1) | Static bearer tokens | — |
| CI/CD | Gitea Actions | — |

### Deviations from Template Defaults

| Template Default | This Repository | Rationale |
|-----------------|-----------------|-----------|
| DynamoDB primary | PostgreSQL serving store | Analytical/slice-oriented workload; see HLD.md#L255-L278 |
| Lambda compute | Containerized HTTP service | Stable query behavior, warm connections; see HLD.md#L296-L308 |
| DynamoDB single-table | Relational with materialized views | Ranking, filtering, grouping, comparison workload |

---

## Component Boundaries

### contracts/ServiceContract/

**Shared interface between the query service and adapters (MCP, HTTP, future).**

- Versioned request/response types for all five tool families
- Storage-agnostic: no table names, partition keys, or column references
- The only code that `service/` and `mcp/` both depend on
- New tool families require a contract version bump

### contracts/ServiceConfiguration/

**Shared configuration model for environment loading.**

- Corpus identity (one deployment per corpus)
- Active dataset version resolution
- Storage backend configuration (S3, PostgreSQL)
- Static token configuration

### service/QueryService/

**The product core. All analytical business logic lives here.**

- Resolves active or requested dataset version
- Executes discovery, aggregation, metadata, drill-down, and evidence queries
- Shapes outputs into compact structured responses
- Applies token-based access control
- Exposes stable HTTP API contracts
- Depends on `contracts/ServiceContract/` and `contracts/ServiceConfiguration/`

### mcp/McpAdapter/

**Thin ChatGPT-facing transport adapter. No analytical logic.**

- Exposes the query service as MCP-compatible tools over Streamable HTTP
- Translates MCP tool calls into query-service requests
- Returns structured tool results
- Depends on `contracts/ServiceContract/`
- Calls `service/QueryService/` via HTTP — no direct database access

### ingestion/IngestionService/

**Receives and registers raw dataset drops.**

- Accepts new dataset deliveries
- Validates structural integrity
- Attaches version identifiers and metadata
- Stores raw snapshots for traceability

### processing/ProcessingService/

**Transforms raw inputs into published analytical views.**

- Normalizes source records into canonical structures
- Derives comparative and aggregate metrics
- Builds category and ecosystem views
- Generates exploration metadata
- Publishes processed outputs for a specific dataset version

### evaluation/EvaluationHarness/

**Answer quality measurement. Not the product — the product's validator.**

- Stores benchmark analyst questions
- Runs question sets against baseline and corpus-enabled workflows
- Compares outputs across dataset versions
- Supports manual and rubric-based scoring

### ops/

**Deployment, publication, and operational scripts.** Infrastructure as code (Terraform) for AWS deployment.

---

## Key Conventions

- **Service-scoped directories**: No source files at top level; all code lives in component subdirectories
- **C# Minimal API**: One endpoint per file, static classes, records for DTOs
- **Interface-based**: All dependencies injected via constructors
- **Immutability**: C# records with `init` for DTOs and domain entities
- **Explicit contracts**: Every component boundary has a documented interface
- **Fail fast**: Validate at boundaries, return clear Problem Details (RFC 7807)

---

## Developer Bootstrap

See [docs/developer-bootstrap.md](docs/developer-bootstrap.md) for local setup.

Quick start:
```bash
dotnet restore
dotnet build
dotnet test
```

---

## Related Documents

- [HLD.md](HLD.md) — High-level design and architectural decisions
- [TECHNICAL.md](TECHNICAL.md) — Technical requirements
- [VISION.md](VISION.md) — Product vision
- [PROBLEM.md](PROBLEM.md) — Problem statement
- [.claude/CLAUDE.md](.claude/CLAUDE.md) — AI assistant context
