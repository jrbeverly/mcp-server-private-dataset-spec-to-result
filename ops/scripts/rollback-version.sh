#!/usr/bin/env bash
# ==============================================================================
# rollback-version.sh — Controlled rollback to a prior dataset version
# ==============================================================================
# Designed for incident response. Lists available versions, confirms the target,
# and atomically rolls back both the serving store and version registry.
#
# Usage:
#   ./rollback-version.sh --version-id <id>     # Rollback to a specific version
#   ./rollback-version.sh --list                 # List versions eligible for rollback
#   ./rollback-version.sh --status              # Show current active version
#
# Environment:
#   STORAGE_POSTGRES_CONNECTION_STRING   PostgreSQL connection string
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

POSTGRES_CONN="${STORAGE_POSTGRES_CONNECTION_STRING:-Host=localhost;Database=corpus;Username=postgres;Password=postgres}"

PSQL_CONN=$(echo "$POSTGRES_CONN" | sed \
    -e 's/Host=/host=/g' \
    -e 's/Database=/dbname=/g' \
    -e 's/Username=/user=/g' \
    -e 's/Password=/password=/g' \
    -e 's/;/ /g')

log()  { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }
fail() { echo "ERROR: $*"; exit 1; }

# -- Commands ------------------------------------------------------------------

cmd_list() {
    echo "=== Available Versions (loaded in serving store) ==="
    psql "$PSQL_CONN" -c "SELECT * FROM serving_list_versions();" 2>/dev/null || {
        echo "Serving store helper functions not installed. Using raw query..."
        psql "$PSQL_CONN" -c "
            SELECT version_id, loaded_at, entity_count, evidence_count,
                   CASE WHEN is_active THEN 'ACTIVE' ELSE 'inactive' END AS status
            FROM serving_version_state
            ORDER BY loaded_at DESC;
        "
    }

    echo ""
    echo "=== Version Registry ==="
    psql "$PSQL_CONN" -c "
        SELECT version_id, status, created_at, updated_at
        FROM dataset_versions
        ORDER BY created_at DESC;
    "
}

cmd_status() {
    echo "=== Current Active Version ==="
    psql "$PSQL_CONN" -c "
        SELECT svs.version_id, svs.loaded_at, svs.entity_count, svs.evidence_count,
               dv.status AS registry_status
        FROM serving_version_state svs
        JOIN dataset_versions dv ON dv.version_id = svs.version_id
        WHERE svs.is_active = true;
    "
}

cmd_rollback() {
    local target="$1"

    # Verify the target version exists and is loaded
    local exists
    exists=$(psql "$PSQL_CONN" -t -c \
        "SELECT EXISTS(SELECT 1 FROM serving_version_state WHERE version_id = '$target')" 2>/dev/null)
    if [[ "$exists" != "t" ]]; then
        fail "Version '$target' is not loaded in the serving store. Use --list to see available versions."
    fi

    # Show what's about to happen
    echo "=== Rollback Summary ==="
    local current
    current=$(psql "$PSQL_CONN" -t -c \
        "SELECT version_id FROM serving_version_state WHERE is_active = true" 2>/dev/null | tr -d '[:space:]')
    echo "  Current active: ${current:-(none)}"
    echo "  Rollback to:    $target"
    echo ""

    # Confirm
    if [[ -t 0 ]]; then
        read -r -p "Proceed with rollback? [y/N] " confirm
        if [[ ! "$confirm" =~ ^[Yy]$ ]]; then
            echo "Rollback cancelled."
            exit 0
        fi
    fi

    log "Rolling back serving store to version '$target'..."
    local result
    result=$(psql "$PSQL_CONN" -t -c "SELECT * FROM serving_rollback_version('$target')" 2>/dev/null)
    echo "$result"

    log "Rolling back version registry..."
    dotnet run --project "$REPO_ROOT/ingestion/IngestionService/src" -- \
        rollback --version-id "$target" \
        || fail "Registry rollback failed"

    log "Rollback complete. Version '$target' is now active."
    cmd_status
}

# -- Argument parsing ----------------------------------------------------------

if [[ $# -eq 0 ]]; then
    echo "Usage:"
    echo "  $0 --list              List available versions"
    echo "  $0 --status            Show current active version"
    echo "  $0 --version-id <id>   Rollback to a specific version"
    exit 1
fi

while [[ $# -gt 0 ]]; do
    case "$1" in
        --list)     cmd_list; exit 0 ;;
        --status)   cmd_status; exit 0 ;;
        --version-id)
            if [[ $# -lt 2 ]]; then
                echo "ERROR: --version-id requires a value"
                exit 1
            fi
            cmd_rollback "$2"
            exit 0
            ;;
        *) echo "Unknown argument: $1"; exit 1 ;;
    esac
done
