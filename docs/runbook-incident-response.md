# Runbook — Incident Response

**Purpose:** Structured response to operational incidents affecting the hosted intelligence service.

**Who:** On-call operator.

## Severity Levels

| Level | Definition | Response Time | Example |
|-------|-----------|---------------|---------|
| **P1 — Critical** | Service unavailable, all requests failing | Immediate | HTTPS endpoint down, database unreachable |
| **P2 — Degraded** | Service available but data quality compromised | Within 4 hours | Wrong dataset version active, missing entities |
| **P3 — Minor** | Non-critical issue, limited impact | Next business day | Single endpoint slow, intermittent errors |

## Incident Response Flow

### 1. Detect

Sources of detection:
- User reports (ChatGPT returns errors or wrong data)
- Health check alerts (HTTPS endpoint returns 5xx)
- Benchmark regression alerts
- CloudWatch log anomalies (spike in 500 errors)

### 2. Triage

Run the diagnostic checklist:

```bash
# Is the service reachable?
curl -s -o /dev/null -w "%{http_code}" https://<service-url>/.well-known/mcp

# Is the MCP endpoint responding?
curl -s -X POST https://<service-url>/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/list","id":1}'

# Is the database reachable?
psql "$PSQL_CONN" -c "SELECT 1"

# Which version is active?
cd ops/scripts && ./rollback-version.sh --status

# What do recent logs show?
aws logs tail /corpus/<corpus-id>/<environment> --follow --filter-pattern "ERROR"
```

### 3. Mitigate

**P1 — Service unavailable:**

1. Check ECS service health:
   ```bash
   aws ecs describe-services --cluster <name> --services <name> --region us-east-1
   ```
2. Check if the database is reachable from the service (security groups, network ACLs)
3. Restart the service if it's in a crash loop:
   ```bash
   aws ecs update-service --cluster <name> --service <name> --force-new-deployment --region us-east-1
   ```
4. Check CloudWatch logs for the crash cause
5. If database is the issue, check RDS or external Postgres provider status

**P2 — Data quality degraded:**

1. Check which version is active: `./rollback-version.sh --status`
2. If a recent publish caused the issue, rollback:
   ```bash
   ./rollback-version.sh --version-id <previous-good-version>
   ```
3. Verify the rollback: `./rollback-version.sh --status`

**P3 — Minor issues:**

1. Check specific endpoint latency in CloudWatch
2. Check database query performance
3. Review recent deploy or config changes

### 4. Resolve

1. Address the root cause (see common scenarios below)
2. Verify the fix
3. Notify stakeholders
4. Update this runbook if the incident revealed a gap

### 5. Post-Incident

1. Document the incident: date, duration, impact, root cause, fix
2. Run benchmarks if data was affected
3. Review whether monitoring could have caught it earlier

## Common Incident Scenarios

### Database connection exhaustion

**Symptoms:** Sporadic 500 errors, increasing latency
**Cause:** Connection pool saturation
**Fix:** Restart the query service (connection pool is per-process). Consider increasing `MaxPoolSize` in the connection string if it recurs.

### Wrong dataset version active

**Symptoms:** Users report stale or incorrect data
**Cause:** Rollback not completed, or publish activated wrong version
**Fix:** `./rollback-version.sh --version-id <correct-version>`

### Token rejected

**Symptoms:** All requests return 401
**Cause:** Token rotated but clients not updated
**Fix:** Distribute new token to users, update connector configuration. If urgent, rotate back to known token.

### S3 artifact access failure

**Symptoms:** Processing pipeline fails, ingestion can't store raw artifacts
**Cause:** IAM role permissions, S3 bucket policy change
**Fix:** Verify IAM role has `s3:PutObject` and `s3:GetObject` on the artifacts bucket. Check bucket policy hasn't changed.
