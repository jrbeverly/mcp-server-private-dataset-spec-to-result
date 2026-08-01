# Contracts — Shared Internal Contract

**The only code shared between `service/` and its adapters.**

## Purpose

Define versioned request/response types for the five analytical tool families. These types form the boundary between the query service core and all adapters (MCP, HTTP, future). Adapters translate external protocols into these contracts; the service implements against them.

## Subdirectories

- **ServiceContract/** — Versioned analytical request/response types (Discovery, Aggregation, Metadata, Drill-down, Raw Evidence)
- **ServiceConfiguration/** — Shared configuration model (corpus identity, storage backends, tokens, dataset versions)

## Rules

- Contract types must NOT leak storage-specific details (no table names, column references, partition keys)
- New tool families require a contract version bump (`V2/`, `V3/`, etc.)
- Adapters depend on contracts; contracts never depend on adapters or the service
- The service depends on contracts for its public API surface
