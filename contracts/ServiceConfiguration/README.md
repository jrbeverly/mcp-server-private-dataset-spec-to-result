# ServiceConfiguration — Shared Configuration Model

**Environment-based configuration for one deployment per corpus.**

## Configuration Sections

| Section | Class | Environment Variables |
|---------|-------|----------------------|
| Corpus identity | `CorpusConfiguration` | `CORPUS_DEPLOYMENT_NAME`, `CORPUS_ID`, `CORPUS_DESCRIPTION` |
| Storage backends | `StorageConfiguration` | `STORAGE_POSTGRES_CONNECTION_STRING`, `STORAGE_S3_BUCKET_NAME`, etc. |
| Token auth | `TokenConfiguration` | `AUTH_TOKEN`, `AUTH_HEADER_NAME` |
| Dataset version | `DatasetVersionConfiguration` | `DATASET_ACTIVE_VERSION`, `DATASET_DEFAULT_VERSION`, etc. |

## One-Call Bootstrap

```csharp
using ServiceConfiguration;

var config = ServiceBootstrap.Load();
// config.Corpus.DeploymentName
// config.Storage.ServingStore.ConnectionString
// config.Auth.IssuedToken
// config.DatasetVersion.ActiveVersion
```

## Design Rules

- **Environment-first**: All config comes from environment variables (12-factor compliance)
- **Fail fast**: Required values throw `InvalidOperationException` on load if missing
- **Immutable records**: Config is read-only after load
- **One deployment per corpus**: `CorpusConfiguration` enforces a single corpus identity per process

## Usage

Both `service/` and `mcp/` reference this project for consistent configuration loading.
