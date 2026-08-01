# Runbook — Token Rotation

**Purpose:** Rotate the static bearer token used for API and MCP endpoint authentication.

**Who:** Operator with deployment access.

**Frequency:** Quarterly, or immediately if a token is suspected of being compromised.

## Prerequisites

- Access to the deployment environment (local, ECS, or SSM Parameter Store)
- `openssl` available for token generation
- AWS CLI configured (for ECS/SSM deployments)

## Step-by-Step

### 1. Generate and apply the new token

```bash
cd ops/scripts

# For local development:
DEPLOYMENT_TARGET=local ./rotate-tokens.sh

# For ECS deployments:
DEPLOYMENT_TARGET=ecs \
  AWS_REGION=us-east-1 \
  ECS_CLUSTER_NAME=corpus-shopify-ecosystem-staging \
  ECS_SERVICE_NAME=corpus-shopify-ecosystem-staging \
  ./rotate-tokens.sh

# For SSM-managed deployments:
DEPLOYMENT_TARGET=ssm \
  CORPUS_ID=shopify-ecosystem \
  ENVIRONMENT=staging \
  AWS_REGION=us-east-1 \
  ./rotate-tokens.sh
```

### 2. Distribute the new token to authorized users

Never send tokens over unencrypted channels. Options:
- Send via encrypted messaging (Signal, iMessage)
- Store in a shared password manager (1Password, Bitwarden)
- Use `gpg` to encrypt: `echo "$NEW_TOKEN" | gpg -e -r user@example.com`

### 3. Update ChatGPT connector configuration

If the MCP connector URL embeds the token:
1. Open ChatGPT → Settings → Developer → Connectors
2. Update the connector URL with the new token
3. Test connectivity

If the token is injected via header:
1. Update the header value in the connector configuration
2. Test connectivity

### 4. Verify

```bash
# Old token should return 401
curl -s -o /dev/null -w "%{http_code}" \
  -X POST https://<service-url>/api/v1/discovery \
  -H "X-Api-Token: <old-token>" \
  -H "Content-Type: application/json" \
  -d '{}'
# Expected: 401

# New token should return 200
curl -s -o /dev/null -w "%{http_code}" \
  -X POST https://<service-url>/api/v1/discovery \
  -H "X-Api-Token: <new-token>" \
  -H "Content-Type: application/json" \
  -d '{}'
# Expected: 200
```

## Compromised Token Protocol

If a token is suspected of being compromised:

1. **Rotate immediately** — follow steps above, do not wait for confirmation
2. **Check access logs** — review CloudWatch logs for unauthorized access patterns
3. **Notify users** — inform authorized users of the rotation
4. **Review scope** — determine what data may have been accessed
5. **Document** — record the incident for future reference

## Token Storage Requirements

- Tokens must never be committed to version control
- Tokens must never appear in logs (the query service redacts them by default)
- Tokens must be stored encrypted at rest (SSM SecureString, sealed secrets)
- Tokens must be transmitted only over TLS
