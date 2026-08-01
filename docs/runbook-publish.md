# Runbook — Dataset Publication

**Purpose:** Controlled publication of a new dataset version to the active serving store.

**Who:** Operator with access to the deployment environment and PostgreSQL database.

**Frequency:** Per dataset refresh cycle (weekly/monthly depending on source data cadence).

## Prerequisites

- Processing pipeline has completed successfully (`ProcessingService process` exited 0)
- Processed artifacts exist at `$ARTIFACT_ROOT_PATH/processed/<version-id>/`
- PostgreSQL is reachable and the serving store schema is initialized
- You have the current `AUTH_TOKEN` for verification

## Step-by-Step

### 1. Verify processed artifacts

```bash
ARTIFACT_ROOT="${ARTIFACT_ROOT_PATH:-/data/artifacts}"
VERSION_ID="2026-05-18-v1"

ls -la "$ARTIFACT_ROOT/processed/$VERSION_ID/"
# Expected: entities.json, evidence.json, manifest.json
```

### 2. Run the publish script

```bash
cd ops/scripts
./publish-version.sh --version-id "$VERSION_ID"
```

The script will:
1. Install/refresh serving store helper functions
2. Process the version through the pipeline (idempotent if already processed)
3. Publish processed artifacts with a manifest
4. Load entities and evidence into the serving store
5. Activate the version (deactivates the previous active version)
6. Verify activation

### 3. Verify the new version is active

```bash
# Check serving store
psql "$PSQL_CONN" -c "SELECT * FROM serving_list_versions();"

# Check version registry
dotnet run --project ../../ingestion/IngestionService/src -- list

# Verify via API
curl -X POST https://<service-url>/api/v1/discovery \
  -H "X-Api-Token: $AUTH_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"limit": 5}' | jq '.datasetVersion'
```

### 4. Run benchmark comparison (if benchmark harness is available)

```bash
dotnet run --project ../../evaluation/EvaluationHarness/src -- \
    run --previous-version <old-version> --target-version "$VERSION_ID"
```

## Rollback If Needed

If the new version produces degraded results:

```bash
./rollback-version.sh --version-id <previous-version>
```

## Failure Modes

| Symptom | Likely Cause | Action |
|---------|-------------|--------|
| `entities.json not found` | Processing pipeline didn't complete | Re-run `ProcessingService process --version-id <id>` |
| `version not found` in serving store | Load step failed | Check psql connectivity, re-run load step manually |
| API returns old version | Activation didn't propagate | Verify `serving_version_state.is_active`, re-run activation |
| Entity count mismatch | Incomplete load | Clear version data and re-load: `DELETE FROM serving_entities WHERE version_id = '<id>'` |

## Environment Variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `STORAGE_POSTGRES_CONNECTION_STRING` | `Host=localhost;Database=corpus;...` | PostgreSQL connection |
| `ARTIFACT_ROOT_PATH` | temp dir | Root path for processed artifacts |
