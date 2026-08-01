#!/usr/bin/env bash
# ==============================================================================
# publish-version.sh — Controlled dataset version publication
# ==============================================================================
# Orchestrates the full publish workflow:
#   1. Run the processing pipeline (normalize, enrich, aggregate)
#   2. Publish processed artifacts
#   3. Load into the serving store
#   4. Activate the new version for query traffic
#
# Usage:
#   ./publish-version.sh --version-id <id> [--source-path <path>] [--skip-ingest]
#
# The version must already be ingested (registered as Raw) unless --source-path
# is provided, in which case ingest runs first.
#
# Environment:
#   STORAGE_POSTGRES_CONNECTION_STRING   PostgreSQL connection string
#   ARTIFACT_ROOT_PATH                    Root path for artifact storage
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# -- Parse arguments -----------------------------------------------------------

VERSION_ID=""
SOURCE_PATH=""
SKIP_INGEST=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version-id) VERSION_ID="$2"; shift 2 ;;
        --source-path) SOURCE_PATH="$2"; shift 2 ;;
        --skip-ingest) SKIP_INGEST=true; shift ;;
        *) echo "Unknown argument: $1"; exit 1 ;;
    esac
done

if [[ -z "$VERSION_ID" ]]; then
    echo "ERROR: --version-id is required"
    echo "Usage: $0 --version-id <id> [--source-path <path>] [--skip-ingest]"
    exit 1
fi

# -- Configuration -------------------------------------------------------------

POSTGRES_CONN="${STORAGE_POSTGRES_CONNECTION_STRING:-Host=localhost;Database=corpus;Username=postgres;Password=postgres}"
ARTIFACT_ROOT="${ARTIFACT_ROOT_PATH:-$(mktemp -d)}"

# Convert Npgsql connection string to psql format
# "Host=X;Database=Y;Username=Z;Password=W" → "host=X dbname=Y user=Z password=W"
PSQL_CONN=$(echo "$POSTGRES_CONN" | sed \
    -e 's/Host=/host=/g' \
    -e 's/Database=/dbname=/g' \
    -e 's/Username=/user=/g' \
    -e 's/Password=/password=/g' \
    -e 's/;/ /g')

log()  { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }
fail() { echo "ERROR: $*"; exit 1; }

# -- Step 1: Ensure helpers are installed --------------------------------------

log "Ensuring serving store helper functions are installed..."
psql "$PSQL_CONN" -f "$REPO_ROOT/ops/sql/serving-store-helpers.sql" -q || fail "Failed to install helper functions"

# -- Step 2: Ingest (if source path provided) ----------------------------------

if [[ -n "$SOURCE_PATH" ]]; then
    log "Step 1/5: Ingesting dataset from $SOURCE_PATH..."
    dotnet run --project "$REPO_ROOT/ingestion/IngestionService/src" -- \
        ingest --source-path "$SOURCE_PATH" \
        || fail "Ingestion failed"
else
    log "Step 1/5: Skipping ingest (--source-path not provided, assuming version exists)"
fi

# -- Step 3: Process (normalize, enrich, aggregate, validate) ------------------

log "Step 2/5: Running processing pipeline for version '$VERSION_ID'..."
dotnet run --project "$REPO_ROOT/processing/ProcessingService/src" -- \
    process --version-id "$VERSION_ID" \
    || fail "Processing failed"

# -- Step 4: Publish artifacts -------------------------------------------------

log "Step 3/5: Publishing processed artifacts..."
dotnet run --project "$REPO_ROOT/processing/ProcessingService/src" -- \
    publish --version-id "$VERSION_ID" \
    || fail "Publication failed"

# -- Step 5: Load into serving store -------------------------------------------

log "Step 4/5: Loading version '$VERSION_ID' into serving store..."

PROCESSED_DIR="$ARTIFACT_ROOT/processed/$VERSION_ID"
ENTITIES_JSON="$PROCESSED_DIR/entities.json"
EVIDENCE_JSON="$PROCESSED_DIR/evidence.json"

if [[ ! -f "$ENTITIES_JSON" ]]; then
    fail "entities.json not found at $ENTITIES_JSON. Processing may have failed silently."
fi

ENTITY_COUNT=$(jq '. | length' "$ENTITIES_JSON" 2>/dev/null || echo "0")
EVIDENCE_COUNT=0
if [[ -f "$EVIDENCE_JSON" ]]; then
    EVIDENCE_COUNT=$(jq '. | length' "$EVIDENCE_JSON" 2>/dev/null || echo "0")
fi

log "  Entities: $ENTITY_COUNT, Evidence records: $EVIDENCE_COUNT"

# Load entities into serving_entities table
if [[ "$ENTITY_COUNT" -gt 0 ]]; then
    log "  Loading entities..."

    # Clear previous load of this version if re-loading
    psql "$PSQL_CONN" -c "DELETE FROM serving_entities WHERE version_id = '$VERSION_ID'" -q

    # Use jq to generate INSERT statements and pipe to psql
    jq -r --arg vid "$VERSION_ID" '
        .[] | @text "INSERT INTO serving_entities (entity_id, name, category, description, properties, metrics, version_id) VALUES (" +
            "'\''\(.entityId // "unknown")'\'', " +
            "'\''\(.name // "Unnamed")'\'', " +
            (if .category then "'\''\(.category)'\'', " else "NULL, " end) +
            (if .description then "'\''\(.description)'\'', " else "NULL, " end) +
            "'\''\(.properties | tostring)'\'', " +
            "'\''\(.metrics | tostring)'\'', " +
            "'\''\($vid)'\''" +
            ") ON CONFLICT (entity_id) DO NOTHING;"
    ' "$ENTITIES_JSON" | psql "$PSQL_CONN" -q || fail "Failed to load entities"
fi

# Load evidence into serving_evidence table
if [[ "$EVIDENCE_COUNT" -gt 0 ]]; then
    log "  Loading evidence..."
    psql "$PSQL_CONN" -c "DELETE FROM serving_evidence WHERE version_id = '$VERSION_ID'" -q

    jq -r --arg vid "$VERSION_ID" '
        .[] | @text "INSERT INTO serving_evidence (record_id, entity_id, type, fields, version_id) VALUES (" +
            "'\''\(.recordId // "unknown")'\'', " +
            "'\''\(.entityId // "")'\'', " +
            "'\''\(.type // "unknown")'\'', " +
            "'\''\(.fields | tostring)'\'', " +
            "'\''\($vid)'\''" +
            ") ON CONFLICT (record_id) DO NOTHING;"
    ' "$EVIDENCE_JSON" | psql "$PSQL_CONN" -q || fail "Failed to load evidence"
fi

# Register version state in serving store
psql "$PSQL_CONN" -c \
    "SELECT serving_upsert_version_state('$VERSION_ID', $ENTITY_COUNT, $EVIDENCE_COUNT)" -q \
    || fail "Failed to upsert version state"

# -- Step 6: Activate ----------------------------------------------------------

log "Step 5/5: Activating version '$VERSION_ID'..."

psql "$PSQL_CONN" -c "SELECT serving_activate_version('$VERSION_ID')" -q \
    || fail "Failed to activate version in serving store"

psql "$PSQL_CONN" -c "SELECT registry_activate_version('$VERSION_ID')" -q \
    || fail "Failed to activate version in registry"

# -- Done ----------------------------------------------------------------------

log "Publication complete."
log "  Version:   $VERSION_ID"
log "  Entities:  $ENTITY_COUNT"
log "  Evidence:  $EVIDENCE_COUNT"
log "  Status:    Active"

# Verify
log "Verifying activation..."
psql "$PSQL_CONN" -c "
    SELECT svs.version_id, svs.entity_count, svs.evidence_count, svs.is_active,
           dv.status AS registry_status
    FROM serving_version_state svs
    JOIN dataset_versions dv ON dv.version_id = svs.version_id
    WHERE svs.version_id = '$VERSION_ID';
"
