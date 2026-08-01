# Runbook — Dataset Rollback

**Purpose:** Controlled rollback from a problematic dataset version to a prior known-good version.

**Who:** On-call operator during an incident, or any team member performing a planned rollback.

**Urgency:** Medium. The service remains available during rollback (serving store activation is atomic).

## Decision Criteria

Rollback when:
- A newly published version produces demonstrably worse benchmark scores
- Entity counts drop unexpectedly (>10% fewer entities)
- Category or metric data is missing or malformed
- Users report incorrect or degraded results

Do NOT rollback when:
- A single entity has incorrect data (fix the source data and re-publish)
- The issue is with the query service or MCP adapter (rollback won't help)
- The active version has been serving correctly for >24 hours (investigate first)

## Prerequisites

- You know which version to rollback TO (run `--list` first)
- The target version is loaded in the serving store
- You have PostgreSQL access

## Step-by-Step

### 1. Identify the current state

```bash
cd ops/scripts
./rollback-version.sh --status
```

Example output:
```
=== Current Active Version ===
 version_id | loaded_at | entity_count | evidence_count | registry_status
------------+-----------+--------------+----------------+-----------------
 v2026-05-18| ...       | 15234        | 89123          | Active
```

### 2. List available rollback targets

```bash
./rollback-version.sh --list
```

Only versions with status `Loaded` or `Active` in the serving store are eligible.

### 3. Verify the target version is suitable

```bash
# Check what data the target version contains
dotnet run --project ../../ingestion/IngestionService/src -- show --version-id <target-version-id>
```

### 4. Execute the rollback

```bash
./rollback-version.sh --version-id <target-version-id>
```

The script will:
1. Show the current active version and the rollback target
2. Ask for confirmation (if running interactively)
3. Deactivate the current version in the serving store
4. Activate the target version in the serving store
5. Update the version registry (marks old as Superseded, target as Active)
6. Display the new active state

### 5. Verify the rollback

```bash
# Confirm the serving store is now serving the target version
./rollback-version.sh --status

# Test the API returns data from the rolled-back version
curl -X POST https://<service-url>/api/v1/discovery \
  -H "X-Api-Token: $AUTH_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"limit": 5}' | jq '.datasetVersion'
```

### 6. Notify stakeholders

```text
Rollback complete: <old-version> → <target-version>
Reason: [brief explanation]
Duration: [how long the bad version was active]
Impact: [any user-visible effects]
Next steps: [fix the bad version, re-ingest, re-publish]
```

## Manual Rollback (if script is unavailable)

```sql
-- 1. Find current active version
SELECT version_id FROM serving_version_state WHERE is_active = true;

-- 2. Deactivate current
UPDATE serving_version_state SET is_active = false WHERE is_active = true;

-- 3. Activate target
UPDATE serving_version_state SET is_active = true WHERE version_id = '<target>';

-- 4. Update registry
SELECT registry_activate_version('<target>');
```

## Post-Rollback Actions

1. **Investigate root cause** — why did the bad version produce degraded results?
2. **Fix the data or processing pipeline** — address the underlying issue
3. **Re-publish a corrected version** — follow the publish runbook
4. **Re-run benchmarks** — compare the corrected version against the current active version
5. **Update the incident log** — document what happened for future reference
