# ServiceContract — Versioned Internal Contract

**The boundary between the query service and all adapters.**

## Versioning

Contracts are versioned by namespace: `ServiceContract.V1`, `ServiceContract.V2`, etc. Each version directory contains the complete set of request/response types for that contract version. New tool families or breaking changes require a version bump.

## V1 Tool Families

| Family | Request | Response | Purpose |
|--------|---------|----------|---------|
| Discovery | `DiscoveryRequest` | `DiscoveryResponse` | Find entities, cohorts, categories |
| Aggregation | `AggregateRequest` | `AggregateResponse` | Counts, rankings, distributions |
| Metadata | `MetadataExplorationRequest` | `MetadataExplorationResponse` | Understand dataset dimensions |
| Drill-down | `DrillDownRequest` | `DrillDownResponse` | Inspect a specific entity |
| Raw Evidence | `RawEvidenceRequest` | `RawEvidenceResponse` | Underlying supporting records |

## Design Rules

- **Storage-agnostic**: No table names, column references, partition keys, or storage implementation details
- **All responses carry version metadata**: Every response includes `DatasetVersion` for provenance
- **Immutable records**: All types use C# `record` with `init`-only properties
- **Compact by design**: Responses are shaped for LLM context windows, not for raw data dumps

## Usage

Both `service/` and `mcp/` reference this project. The service implements against these types; adapters call through them.

```csharp
using ServiceContract.V1;

// Adapter side (e.g., MCP tool handler)
var request = new DiscoveryRequest { Query = "competitor analysis", Limit = 10 };
var response = await _queryServiceClient.DiscoverAsync(request);

// Service side (analytical logic)
public async Task<DiscoveryResponse> DiscoverAsync(DiscoveryRequest request)
{
    // Business logic here — filtering, ranking, enrichment
    return new DiscoveryResponse { ... };
}
```

## Adding a V2

When new tool families or breaking changes are needed:

1. Create `V2/` directory alongside `V1/`
2. Copy and evolve types — do NOT modify V1 types in place
3. Add a `IVersionedContract` marker if runtime dispatch is needed
4. Both versions remain supported until all callers migrate

## Related

- [HLD.md: Service Contract Strategy](../../../HLD.md#L417-L433)
- [CODEMAP.md: Component Boundaries](../../../CODEMAP.md)
