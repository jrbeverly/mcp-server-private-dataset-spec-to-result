# MCP Adapter — ChatGPT Integration Layer

**Thin transport adapter. No analytical logic allowed.**

This adapter translates MCP tool invocations into query-service requests and returns structured tool results. It is a protocol bridge — not the product.

## Responsibilities

- Expose the query service as an MCP-compatible tool surface over Streamable HTTP
- Advertise tool metadata that helps model discovery and invocation
- Translate MCP tool calls into HTTP requests against `service/QueryService/`
- Return structured tool results in ChatGPT-friendly format
- Handle MCP transport concerns (session management, JSON-RPC framing)

## What this component MUST NOT do

- Filter, rank, aggregate, or enrich results (belongs to `service/`)
- Access the serving store directly (go through `service/`)
- Define analytical business rules
- Transform data beyond protocol-required formatting

## Tool Surface

Each tool family from `contracts/ServiceContract/` becomes an MCP tool:

| MCP Tool | Maps to | Description |
|----------|---------|-------------|
| `discover` | `POST /api/v1/discovery` | Find entities, cohorts, categories |
| `aggregate` | `POST /api/v1/aggregation` | Counts, rankings, distributions |
| `explore_metadata` | `POST /api/v1/metadata` | Understand dataset dimensions |
| `drill_down` | `POST /api/v1/drill-down` | Inspect a specific entity |
| `get_evidence` | `POST /api/v1/raw-evidence` | Underlying supporting records |

## Transport

- **Protocol:** MCP over Streamable HTTP (per OpenAI Apps SDK recommendation)
- **Endpoint:** `/.well-known/mcp` (or `/mcp`)
- **Auth:** Static bearer token injected via connector URL tokenization

## Project Structure

```
mcp/McpAdapter/
├── src/
│   ├── McpAdapter.csproj
│   ├── Program.cs              # MCP server host
│   ├── Tools/                  # One file per tool (thin wrappers)
│   │   ├── DiscoverTool.cs
│   │   ├── AggregateTool.cs
│   │   ├── ExploreMetadataTool.cs
│   │   ├── DrillDownTool.cs
│   │   └── GetEvidenceTool.cs
│   └── Infrastructure/         # HTTP client to query service
│       └── QueryServiceClient.cs
```

## Hard Rule

If you find yourself writing filter logic, ranking, aggregation, or enrichment in this component — **stop and move it to `service/QueryService.Domain/`.**

## Dependencies

- `contracts/ServiceContract/` — request/response types
- `contracts/ServiceConfiguration/` — token and endpoint configuration
- `service/QueryService/` — via HTTP (not via project reference)

## Related

- [HLD.md: MCP Adapter](../../../HLD.md#L211-L221)
- [HLD.md: ChatGPT Connector Design Direction](../../../HLD.md#L403-L413)
