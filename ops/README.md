# Operations — Deployment and Publication

**Infrastructure as code, deployment automation, and operational scripts.**

## Responsibilities

- Terraform configurations for AWS deployment (one deployment per corpus)
- Dockerfiles for containerized service deployment
- Dataset publication scripts (trigger processing, activate new version, rollback)
- Token rotation and local development environment provisioning
- SQL helper functions for serving store lifecycle operations

## Structure

```
ops/
├── terraform/                    # AWS infrastructure (Terraform)
│   └── corpus-deployment/        # One deployment = one corpus
│       ├── main.tf               # S3, RDS (optional), ECS, ALB, IAM, SSM
│       ├── variables.tf           # All configurable inputs
│       ├── outputs.tf             # Deployment outputs for scripting
│       └── terraform.tfvars.example
├── docker/                       # Container images
│   ├── Dockerfile.query-service   # Query service production image
│   ├── Dockerfile.mcp-adapter     # MCP adapter production image
│   ├── Dockerfile.processing-service  # Processing batch job image
│   └── docker-compose.yml        # Full local stack (Postgres + services)
├── sql/                          # Database helpers
│   └── serving-store-helpers.sql # Version lifecycle functions
├── scripts/                      # Operational scripts
│   ├── publish-version.sh        # Full publish workflow (process → load → activate)
│   ├── rollback-version.sh       # Rollback to a prior dataset version
│   ├── ingest.sh                 # Accept a new dataset delivery
│   ├── rotate-tokens.sh          # Rotate static access tokens
│   └── local-bootstrap.sh        # One-command local environment setup
└── README.md                     # This file
```

## Deployment Model

- One Terraform workspace per corpus/environment combination
- Infrastructure changes are manual and infrequent (quarterly cadence)
- Application deployments are separate from infrastructure changes
- Container images built and pushed independently of Terraform applies
- Dataset publications do not require service redeployment

### Standing Up a Second Corpus

A second corpus is a second deployment — not multi-tenant logic:

```bash
# Create a new Terraform workspace
cd ops/terraform/corpus-deployment
terraform workspace new second-corpus

# Apply with the new corpus configuration
terraform apply \
  -var="corpus_id=second-corpus" \
  -var="deployment_name=Second Corpus Intelligence" \
  -var="postgres_connection_string=..." \
  -var="auth_token=$(openssl rand -hex 32)" \
  -var-file="production.tfvars"
```

Each corpus gets its own S3 bucket, ECS service, database, and token namespace. No shared state between corpora.

## Quick Start (Local)

```bash
# One command to bootstrap everything
./ops/scripts/local-bootstrap.sh

# Or step by step:
docker compose -f ops/docker/docker-compose.yml up -d
./ops/scripts/ingest.sh --source-path /path/to/dataset.csv
./ops/scripts/publish-version.sh --version-id <id> --source-path /path/to/dataset.csv
```

## Related

- [HLD.md: Storage and Infrastructure Direction](../HLD.md#L238-L280)
- [docs/runbook-publish.md](../docs/runbook-publish.md)
- [docs/runbook-rollback.md](../docs/runbook-rollback.md)
- [docs/runbook-token-rotation.md](../docs/runbook-token-rotation.md)
- [docs/runbook-incident-response.md](../docs/runbook-incident-response.md)
- [docs/runbook-benchmark-rerun.md](../docs/runbook-benchmark-rerun.md)
