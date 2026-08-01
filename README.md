# MCP Server Private Static Dataset

> [!WARNING]
> **AI-authored:** This change was autonomously planned and implemented by an AI software factory from a human-authored specification, with possible subsequent human review or modification.

> [!WARNING]
> This experiment is effectively abandoned. The generated material is retained primarily as a research artifact.

A hosted service that gives large language models access to curated, structured commercial datasets through an MCP-compatible integration layer. It lets ChatGPT and other MCP clients run analytical queries — discovery, aggregation, drill-down, metadata exploration, and raw evidence retrieval — against a versioned, access-controlled data corpus.

The system is designed for one deployment per intelligence corpus. Each deployment packages a query service (the analytical core), a thin MCP transport adapter, and batch ingestion and processing pipelines. The MCP adapter translates tool calls into HTTP requests against the query service, which resolves dataset versions and executes queries against a PostgreSQL serving store. All access is gated behind static bearer tokens.

```bash
make setup
make build
make test

docker compose -f ops/docker/docker-compose.yml up -d
```

Verify:

```bash
curl -X POST http://localhost:5000/api/v1/discovery \
  -H "X-Api-Token: dev-token" \
  -H "Content-Type: application/json" \
  -d '{"query": "example", "limit": 5}'

curl http://localhost:8080/.well-known/mcp
```

## Notes

- Although the factory achieved the end state, it did so with a significant amount of code
- Technical design achieved, but with substantial implementation volume.
- Heavy use of processing, orchestration, and data-translation scaffolding.
- Individual components are often simple; aggregate system complexity is high.
- Question whether scaffolding cost is justified by delivered value.
- Small, discrete components improve comprehensibility, attestation, benchmarking, and health assessment.
- Modularity is a strength; overall architecture still feels overbuilt.
- Strong “AI slog” character: large volume, low information density.
- ~500-line HLD encourages skimming rather than comprehension.
- Valuable technical content is sparse relative to document size.
- Original inputs suffered from the same excessive-writing / low-density problem.
- Architecture does not inspire confidence as a deployable static MCP for private datasets.
- Poor fit for the intended “publish private/static data through MCP” use case.
- Comparable dataset-serving use cases should likely require materially less machinery.
- Current design creates strong reluctance to deploy or build on top of it.
- Some issues may be stylistic consequences of insufficiently opinionated design constraints.
- Core concern remains architectural, not merely stylistic: this appears to be the wrong approach.

Although the factory has _technically_ realized exactly what was sought after when it came to the static MCP, I think the spirit of it is lacking. The original intention was to explore avenues for an _as simple as possible_ enhanced search, but in practice it has tended towards _an absolute ton of code_.
