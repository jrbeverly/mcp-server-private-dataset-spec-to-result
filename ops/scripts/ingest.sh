#!/usr/bin/env bash
# ==============================================================================
# ingest.sh — Accept and register a new dataset delivery
# ==============================================================================
# Validates the dataset structure, stores the raw snapshot, and registers
# a new version in the version registry.
#
# Usage:
#   ./ingest.sh --source-path <path> [--metadata <json>]
#
# Environment:
#   STORAGE_POSTGRES_CONNECTION_STRING   PostgreSQL connection string
#   ARTIFACT_ROOT_PATH                    Root path for artifact storage
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

SOURCE_PATH=""
METADATA=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --source-path) SOURCE_PATH="$2"; shift 2 ;;
        --metadata) METADATA="$2"; shift 2 ;;
        *) echo "Unknown argument: $1"; exit 1 ;;
    esac
done

if [[ -z "$SOURCE_PATH" ]]; then
    echo "ERROR: --source-path is required"
    echo "Usage: $0 --source-path <path> [--metadata <json>]"
    exit 1
fi

if [[ ! -f "$SOURCE_PATH" && ! -d "$SOURCE_PATH" ]]; then
    echo "ERROR: Source path does not exist: $SOURCE_PATH"
    exit 1
fi

echo "[$(date '+%Y-%m-%d %H:%M:%S')] Ingesting dataset from $SOURCE_PATH..."

ARGS=("--source-path" "$SOURCE_PATH")
if [[ -n "$METADATA" ]]; then
    ARGS+=("--metadata" "$METADATA")
fi

dotnet run --project "$REPO_ROOT/ingestion/IngestionService/src" -- \
    ingest "${ARGS[@]}" \
    || { echo "ERROR: Ingestion failed"; exit 1; }

echo "[$(date '+%Y-%m-%d %H:%M:%S')] Ingestion complete."
