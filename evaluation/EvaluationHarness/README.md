# Evaluation — Answer Quality Harness

**Validates that the corpus materially improves analytical answers.**

## Responsibilities

- Store benchmark analyst questions (the evaluation set)
- Run the same question sets against baseline (no corpus) and corpus-enabled workflows
- Compare outputs across dataset versions
- Support manual and rubric-based scoring
- Track answer quality regression across dataset versions

## What this component does NOT do

- Run production queries (use `service/` directly)
- Modify the dataset or serving views
- Replace human judgment in evaluation

## Entry Points

- Benchmark question storage and management
- Automated batch evaluation runs
- Scoring and comparison tooling

## Dependencies

- `contracts/ServiceContract/` — request/response types
- `contracts/ServiceConfiguration/` — endpoint and token configuration
- `service/QueryService/` — via HTTP (same interface as MCP adapter)

## Workflow

1. Load benchmark questions
2. Execute each question against the query service API
3. Capture structured responses with version metadata
4. Score responses (rubric-based or manual)
5. Compare scores across dataset versions
6. Flag regressions where answer quality drops

## Related

- [HLD.md: Evaluation Harness](../../../HLD.md#L225-L236)
