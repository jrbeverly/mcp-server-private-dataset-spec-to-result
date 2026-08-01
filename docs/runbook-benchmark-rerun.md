# Runbook — Benchmark Rerun

**Purpose:** Re-run the evaluation benchmark suite after a dataset version change, processing pipeline change, or service update.

**Who:** Operator or developer.

**Frequency:** After every dataset publish, after pipeline changes, before major releases.

## Prerequisites

- Evaluation harness built and available
- Both baseline and corpus-enabled workflows accessible
- Benchmark questions defined (default set in `evaluation/EvaluationHarness/src/Questions/`)

## Step-by-Step

### 1. Run benchmarks against the current active version

```bash
cd evaluation/EvaluationHarness/src

# Run with default question set against the active dataset version
dotnet run -- run \
    --query-service-url "$QUERY_SERVICE_URL" \
    --api-token "$AUTH_TOKEN" \
    --output-dir ../../benchmark-results
```

### 2. Compare against a prior run (if available)

```bash
dotnet run -- compare \
    --baseline-run ../../benchmark-results/<previous-run-id>.json \
    --target-run ../../benchmark-results/<new-run-id>.json
```

### 3. Review results

Key metrics to check:
- **Tool surface coverage:** All five tool families produce valid responses
- **Response shaping:** Responses include required fields (DatasetVersion, entities/buckets)
- **Score regression:** Any score decrease compared to baseline
- **Missing entities:** Entity count changes that may indicate data loss

### 4. Interpret results

**Scores improved or stable:** The new version is good. No action needed.

**Scores regressed (>5% drop):**
1. Check if entity counts changed significantly
2. Verify the processing pipeline ran correctly
3. Compare entity data between versions:
   ```bash
   dotnet run --project ../../ingestion/IngestionService/src -- show --version-id <old>
   dotnet run --project ../../ingestion/IngestionService/src -- show --version-id <new>
   ```
4. Consider rollback if regression is severe: `../../ops/scripts/rollback-version.sh --version-id <old>`

**Entity count dropped >10%:**
- This may indicate an ingestion or processing issue
- Compare raw source data between versions
- Check processing logs for errors

### 5. Store results

Keep benchmark results for trend analysis:
```bash
# Results are stored in the output directory with timestamped run IDs
ls ../../benchmark-results/
```

## Running Benchmarks After Publishing

The complete post-publish validation flow:

```bash
# 1. Publish the new version
cd ops/scripts
./publish-version.sh --version-id "v2026-05-18"

# 2. Run benchmarks
cd ../../evaluation/EvaluationHarness/src
dotnet run -- run \
    --query-service-url "$QUERY_SERVICE_URL" \
    --api-token "$AUTH_TOKEN" \
    --output-dir ../../benchmark-results

# 3. Compare with previous version
dotnet run -- compare \
    --baseline-run ../../benchmark-results/<previous-run>.json \
    --target-run ../../benchmark-results/<new-run>.json

# 4. If degraded, rollback
cd ../../ops/scripts
./rollback-version.sh --version-id <previous-version>
```
