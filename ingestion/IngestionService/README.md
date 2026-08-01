# Ingestion — Raw Dataset Import

**Accepts synchronized data drops and registers them as versioned snapshots.**

## Responsibilities

- Accept new dataset deliveries from external acquisition or sync workflows
- Validate basic structural integrity (schema conformance, required fields)
- Attach version identifiers and publication metadata (timestamp, source, checksum)
- Store raw snapshots in versioned object storage (S3) for traceability and rollback

## What this component does NOT do

- Normalization or enrichment (belongs to `processing/`)
- Serving queries (belongs to `service/`)
- Format conversion (belongs to `processing/`)

## Entry Points

- Version registration and metadata attachment
- Raw snapshot storage with integrity verification

## Dependencies

- `contracts/ServiceContract/` — for version metadata types
- `contracts/ServiceConfiguration/` — for storage backend configuration
