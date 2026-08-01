#!/usr/bin/env bash
# ==============================================================================
# rotate-tokens.sh — Rotate static access tokens
# ==============================================================================
# Generates a new cryptographically random token, updates the deployment
# configuration, and restarts the affected service.
#
# Usage:
#   ./rotate-tokens.sh                         # Generate and apply a new token
#   ./rotate-tokens.sh --token <value>         # Set a specific token value
#   ./rotate-tokens.sh --dry-run               # Generate but don't apply
#
# Environment:
#   DEPLOYMENT_TARGET                        local | ecs | ssm
#   STORAGE_POSTGRES_CONNECTION_STRING       For local token verification
#   AWS_REGION                               For ECS/SSM deployments
#   ECS_CLUSTER_NAME                         For ECS deployments
#   ECS_SERVICE_NAME                         For ECS deployments
# ==============================================================================

set -euo pipefail

DEPLOYMENT_TARGET="${DEPLOYMENT_TARGET:-local}"
DRY_RUN=false
FORCE_TOKEN=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --dry-run) DRY_RUN=true; shift ;;
        --token) FORCE_TOKEN="$2"; shift 2 ;;
        *) echo "Unknown argument: $1"; exit 1 ;;
    esac
done

log()  { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }
fail() { echo "ERROR: $*"; exit 1; }

# -- Generate new token --------------------------------------------------------

if [[ -n "$FORCE_TOKEN" ]]; then
    NEW_TOKEN="$FORCE_TOKEN"
    log "Using provided token value"
else
    NEW_TOKEN=$(openssl rand -hex 32)
    log "Generated new token"
fi

echo "  Token: ${NEW_TOKEN:0:8}...${NEW_TOKEN: -8}"

if $DRY_RUN; then
    log "Dry run — token not applied."
    log "Set DEPLOYMENT_TARGET and run without --dry-run to apply."
    exit 0
fi

# -- Apply based on deployment target ------------------------------------------

case "$DEPLOYMENT_TARGET" in
    local)
        log "Updating local environment..."
        log "Set AUTH_TOKEN=$NEW_TOKEN in your environment."
        log "For docker-compose: update the AUTH_TOKEN value in docker-compose.yml and run:"
        log "  docker compose -f ops/docker/docker-compose.yml up -d --force-recreate"

        # Verify local Postgres connectivity if available
        POSTGRES_CONN="${STORAGE_POSTGRES_CONNECTION_STRING:-}"
        if [[ -n "$POSTGRES_CONN" ]]; then
            PSQL_CONN=$(echo "$POSTGRES_CONN" | sed \
                -e 's/Host=/host=/g' -e 's/Database=/dbname=/g' \
                -e 's/Username=/user=/g' -e 's/Password=/password=/g' \
                -e 's/;/ /g')
            if psql "$PSQL_CONN" -c "SELECT 1" -q &>/dev/null; then
                log "Local Postgres connection verified."
            fi
        fi
        ;;

    ecs)
        log "Updating ECS service..."
        AWS_REGION="${AWS_REGION:-us-east-1}"
        ECS_CLUSTER="${ECS_CLUSTER_NAME:?ECS_CLUSTER_NAME must be set for ECS deployments}"
        ECS_SERVICE="${ECS_SERVICE_NAME:?ECS_SERVICE_NAME must be set for ECS deployments}"

        # Update the task definition with the new token
        TASK_DEF_ARN=$(aws ecs describe-task-definition \
            --task-definition "corpus-${ECS_CLUSTER#corpus-}" \
            --region "$AWS_REGION" \
            --query 'taskDefinition.taskDefinitionArn' \
            --output text 2>/dev/null) || fail "Failed to describe task definition"

        NEW_TASK_DEF=$(aws ecs describe-task-definition \
            --task-definition "$TASK_DEF_ARN" \
            --region "$AWS_REGION" \
            --query 'taskDefinition' \
            2>/dev/null) || fail "Failed to get task definition"

        # Update the AUTH_TOKEN environment variable
        UPDATED_DEF=$(echo "$NEW_TASK_DEF" | jq \
            --arg token "$NEW_TOKEN" \
            '.containerDefinitions[0].environment = (.containerDefinitions[0].environment | map(if .name == "AUTH_TOKEN" then .value = $token else . end))')

        # Register new task definition
        NEW_ARN=$(aws ecs register-task-definition \
            --family "$(echo "$UPDATED_DEF" | jq -r '.family')" \
            --container-definitions "$(echo "$UPDATED_DEF" | jq -c '.containerDefinitions')" \
            --execution-role-arn "$(echo "$UPDATED_DEF" | jq -r '.executionRoleArn')" \
            --task-role-arn "$(echo "$UPDATED_DEF" | jq -r '.taskRoleArn // ""')" \
            --network-mode "$(echo "$UPDATED_DEF" | jq -r '.networkMode')" \
            --requires-compatibilities "$(echo "$UPDATED_DEF" | jq -r '.requiresCompatibilities | join(",")')" \
            --cpu "$(echo "$UPDATED_DEF" | jq -r '.cpu')" \
            --memory "$(echo "$UPDATED_DEF" | jq -r '.memory')" \
            --region "$AWS_REGION" \
            --query 'taskDefinition.taskDefinitionArn' \
            --output text 2>/dev/null) || fail "Failed to register new task definition"

        # Update ECS service with new task definition
        aws ecs update-service \
            --cluster "$ECS_CLUSTER" \
            --service "$ECS_SERVICE" \
            --task-definition "$NEW_ARN" \
            --region "$AWS_REGION" \
            --no-cli-pager &>/dev/null || fail "Failed to update ECS service"

        log "ECS service updated with new token. Waiting for deployment..."
        aws ecs wait services-stable \
            --cluster "$ECS_CLUSTER" \
            --services "$ECS_SERVICE" \
            --region "$AWS_REGION" || log "Warning: deployment may still be in progress"
        ;;

    ssm)
        log "Updating SSM Parameter Store..."
        AWS_REGION="${AWS_REGION:-us-east-1}"
        CORPUS_ID="${CORPUS_ID:?CORPUS_ID must be set}"
        ENVIRONMENT="${ENVIRONMENT:-staging}"

        aws ssm put-parameter \
            --name "/corpus/${CORPUS_ID}/${ENVIRONMENT}/AUTH_TOKEN" \
            --value "$NEW_TOKEN" \
            --type "SecureString" \
            --overwrite \
            --region "$AWS_REGION" \
            --no-cli-pager || fail "Failed to update SSM parameter"

        log "SSM parameter updated. Restart ECS service to pick up the new token."
        log "  aws ecs update-service --cluster <name> --service <name> --force-new-deployment --region $AWS_REGION"
        ;;

    *)
        fail "Unknown DEPLOYMENT_TARGET: $DEPLOYMENT_TARGET (use: local, ecs, ssm)"
        ;;
esac

log "Token rotation complete."
log "Old token is now invalid. Update any clients with the new token."
