#!/usr/bin/env bash
# ==============================================================================
# local-bootstrap.sh — One-command local development environment setup
# ==============================================================================
# Bootstraps a complete local deployment:
#   - Verifies prerequisites (.NET SDK, Docker, jq)
#   - Restores NuGet packages and builds all projects
#   - Starts PostgreSQL via Docker
#   - Initializes database schemas
#   - Generates a local dev token
#   - Starts the query service and MCP adapter
#
# Usage:
#   ./local-bootstrap.sh              # Full setup
#   ./local-bootstrap.sh --skip-db    # Skip database initialization
#   ./local-bootstrap.sh --prod-like  # Use stricter settings (no version pinning)
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

SKIP_DB=false
PROD_LIKE=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-db) SKIP_DB=true; shift ;;
        --prod-like) PROD_LIKE=true; shift ;;
        *) echo "Unknown argument: $1"; exit 1 ;;
    esac
done

log()  { echo "[bootstrap] $*"; }
fail() { echo "ERROR: $*"; exit 1; }

# -- Prerequisites -------------------------------------------------------------

command -v dotnet >/dev/null 2>&1 || fail ".NET SDK is required (https://dotnet.microsoft.com/download)"
command -v docker >/dev/null 2>&1 || fail "Docker is required (https://docs.docker.com/get-docker)"
command -v jq >/dev/null 2>&1 || fail "jq is required (https://stedolan.github.io/jq)"

log "Prerequisites satisfied."
log ".NET SDK: $(dotnet --version)"

# -- Build ---------------------------------------------------------------------

log "Restoring NuGet packages..."
dotnet restore "$REPO_ROOT" --verbosity quiet

log "Building all projects..."
dotnet build "$REPO_ROOT" --no-restore --verbosity quiet

# -- Database ------------------------------------------------------------------

POSTGRES_CONN="${STORAGE_POSTGRES_CONNECTION_STRING:-Host=localhost;Database=corpus;Username=postgres;Password=postgres}"
PSQL_CONN=$(echo "$POSTGRES_CONN" | sed \
    -e 's/Host=/host=/g' \
    -e 's/Database=/dbname=/g' \
    -e 's/Username=/user=/g' \
    -e 's/Password=/password=/g' \
    -e 's/;/ /g')

if ! $SKIP_DB; then
    log "Starting PostgreSQL..."
    docker run -d --name corpus-postgres-dev \
        -e POSTGRES_DB=corpus \
        -e POSTGRES_USER=postgres \
        -e POSTGRES_PASSWORD=postgres \
        -p 5432:5432 \
        postgres:16-alpine 2>/dev/null || log "PostgreSQL container already running (corpus-postgres-dev)"

    # Wait for Postgres to be ready
    log "Waiting for PostgreSQL to be ready..."
    for i in $(seq 1 30); do
        if psql "$PSQL_CONN" -c "SELECT 1" -q &>/dev/null; then
            log "PostgreSQL is ready."
            break
        fi
        sleep 1
    done

    # Initialize schemas
    log "Initializing database schemas..."
    dotnet run --project "$REPO_ROOT/ingestion/IngestionService/src" -- init-db \
        || fail "Failed to initialize ingestion schema"

    dotnet run --project "$REPO_ROOT/processing/ProcessingService/src" -- init-db \
        || fail "Failed to initialize processing schema"

    # Ensure QueryService serving store schema exists
    log "Initializing serving store schema..."
    psql "$PSQL_CONN" -c "
        CREATE TABLE IF NOT EXISTS serving_entities (
            entity_id VARCHAR(256) NOT NULL,
            name VARCHAR(512) NOT NULL,
            category VARCHAR(256),
            description TEXT,
            properties JSONB NOT NULL DEFAULT '{}',
            metrics JSONB NOT NULL DEFAULT '{}',
            version_id VARCHAR(64) NOT NULL,
            PRIMARY KEY (entity_id, version_id)
        );

        CREATE TABLE IF NOT EXISTS serving_evidence (
            record_id VARCHAR(256) NOT NULL,
            entity_id VARCHAR(256) NOT NULL,
            type VARCHAR(128) NOT NULL,
            fields JSONB NOT NULL DEFAULT '{}',
            source_timestamp TIMESTAMP WITH TIME ZONE,
            version_id VARCHAR(64) NOT NULL,
            PRIMARY KEY (record_id, version_id)
        );

        CREATE TABLE IF NOT EXISTS serving_version_state (
            version_id VARCHAR(64) PRIMARY KEY,
            loaded_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
            entity_count INTEGER NOT NULL DEFAULT 0,
            evidence_count INTEGER NOT NULL DEFAULT 0,
            is_active BOOLEAN NOT NULL DEFAULT false
        );
    " -q || fail "Failed to initialize serving store schema"

    # Install helper functions
    psql "$PSQL_CONN" -f "$REPO_ROOT/ops/sql/serving-store-helpers.sql" -q \
        || fail "Failed to install helper functions"

    log "Database schemas initialized."
else
    log "Skipping database initialization (--skip-db)."
fi

# -- Tokens --------------------------------------------------------------------

DEV_TOKEN="${AUTH_TOKEN:-$(openssl rand -hex 16)}"
log "Auth token: $DEV_TOKEN"

# -- Environment ---------------------------------------------------------------

export CORPUS_DEPLOYMENT_NAME="${CORPUS_DEPLOYMENT_NAME:-local-dev}"
export CORPUS_ID="${CORPUS_ID:-local-corpus}"
export AUTH_TOKEN="$DEV_TOKEN"
export AUTH_HEADER_NAME="${AUTH_HEADER_NAME:-X-Api-Token}"
export STORAGE_POSTGRES_CONNECTION_STRING="$POSTGRES_CONN"
export STORAGE_S3_BUCKET_NAME="${STORAGE_S3_BUCKET_NAME:-local-artifacts}"
export ARTIFACT_ROOT_PATH="${ARTIFACT_ROOT_PATH:-$(mktemp -d)}"
export DATASET_ALLOW_VERSION_PINNING="${DATASET_ALLOW_VERSION_PINNING:-true}"

if $PROD_LIKE; then
    export DATASET_ALLOW_VERSION_PINNING="false"
fi

mkdir -p "$ARTIFACT_ROOT_PATH/processed"

# -- Run -----------------------------------------------------------------------

log "Starting query service on http://localhost:5000..."
dotnet run --project "$REPO_ROOT/service/QueryService/src/QueryService.Api" &
QUERY_PID=$!
log "Query service PID: $QUERY_PID"

sleep 3

log "Starting MCP adapter on http://localhost:8080..."
export QUERY_SERVICE_URL="http://localhost:5000"
dotnet run --project "$REPO_ROOT/mcp/McpAdapter/src" &
MCP_PID=$!
log "MCP adapter PID: $MCP_PID"

sleep 2

# -- Verify --------------------------------------------------------------------

log "Verifying services..."
if curl -sf http://localhost:8080/.well-known/mcp >/dev/null 2>&1; then
    log "MCP adapter is responding."
else
    log "Warning: MCP adapter health check failed."
fi

# -- Summary -------------------------------------------------------------------

echo ""
echo "============================================"
echo "  Local Deployment Ready"
echo "============================================"
echo ""
echo "  Query Service:  http://localhost:5000"
echo "  MCP Adapter:    http://localhost:8080"
echo "  MCP Endpoint:   http://localhost:8080/mcp"
echo "  Well-Known:     http://localhost:8080/.well-known/mcp"
echo "  Auth Token:     $DEV_TOKEN"
echo "  Corpus ID:      $CORPUS_ID"
echo ""
echo "  Test discovery:"
echo "  curl -X POST http://localhost:5000/api/v1/discovery \\"
echo "    -H 'X-Api-Token: $DEV_TOKEN' \\"
echo "    -H 'Content-Type: application/json' \\"
echo "    -d '{\"limit\": 5}'"
echo ""
echo "  Stop services: kill $QUERY_PID $MCP_PID"
echo ""
echo "============================================"
