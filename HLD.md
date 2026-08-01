# High-Level Design

## Purpose

This document defines the intended v1 technical direction for the hosted intelligence service described in [PROBLEM.md](PROBLEM.md), [TECHNICAL.md](TECHNICAL.md), [VISION.md](VISION.md), and refined in [MOCK-DESIGN.md](MOCK-DESIGN.md).

The goal of this HLD is to move from exploration into an implementation-ready direction. It does not define detailed schemas or low-level endpoint contracts, but it does define what we are building, how the main pieces fit together, and which architectural choices are now considered part of the plan.

## V1 Decision Summary

The first implementation will be:

- a narrow, deployment-specific hosted intelligence service
- centered on one shared global corpus per deployment
- optimized for individual analyst workflows
- designed for ChatGPT first
- able to support DeepSeek or other clients later without changing the core service
- read-heavy, exploration-heavy, and batch-updated
- protected by simple static access tokens
- evaluated primarily on answer quality

The first build is not a generalized multi-tenant platform. It is a focused system for exposing one structured intelligence corpus to AI-assisted analyst workflows.

## Chosen Technical Direction

The selected direction for v1 is:

- a versioned hosted corpus
- a preprocessing pipeline that turns source data into analytical serving views
- a protocol-agnostic query service that owns the business logic
- a thin ChatGPT-facing MCP adapter over Streamable HTTP
- a small family of analytics-oriented tools rather than a generic search or raw database interface
- a private connector onboarding model for ChatGPT rather than a public app marketplace launch

This direction keeps the core system useful even if the ChatGPT integration path changes later.

## Why This Direction

This direction is the best fit for the requirements for three reasons.

First, the workload is analytical rather than transactional. The important tasks are category exploration, trend discovery, ecosystem understanding, comparator discovery, and drill-down. That favors a service that exposes curated analytical operations over a structured corpus, not a thin CRUD or lookup API.

Second, ChatGPT is the primary launch target. The current official Apps SDK direction uses MCP to connect apps to ChatGPT, requires an MCP server, and allows tool-only integrations without a custom UI. It also supports private testing through developer mode and connector creation against an HTTPS `/mcp` endpoint. That makes a private MCP connector a practical first integration path for v1.

Third, the product still needs to stay portable. Even though ChatGPT is first, the service should not be designed as ChatGPT-only logic. The intelligence-serving core should remain independently useful so that DeepSeek, Claude, or a direct HTTP client can be supported later through additional adapters.

## What We Are Building

The v1 system is a hosted analytical intelligence service with four major capabilities:

- ingest versioned source datasets
- preprocess them into AI-usable analytical views
- serve discovery, aggregation, metadata exploration, drill-down, and raw evidence retrieval
- expose that functionality to ChatGPT through a private MCP connector

The analyst experience should look like this:

1. A dataset version is synchronized and published.
2. An analyst connects the service to ChatGPT using a private connector URL.
3. The analyst asks broad ecosystem questions.
4. ChatGPT calls tools exposed by the service.
5. The service returns structured results and supporting evidence.
6. The analyst uses the response to continue exploration and drill into promising leads.

## Out Of Scope For V1

The first implementation will not attempt to provide:

- multi-tenant customer-specific datasets
- real-time ingestion or streaming updates
- sophisticated anti-exfiltration controls
- a public ChatGPT app rollout
- a polished end-user dashboard
- a generalized query language for arbitrary workloads
- full billing infrastructure beyond token-gated access

Those may become relevant later, but they are not required to validate the core product.

## Primary Integration Strategy

### ChatGPT First

ChatGPT is the primary v1 client.

The first integration should be a private ChatGPT connector using MCP over HTTPS. The service will expose a public `/mcp` endpoint that a user can add in ChatGPT developer mode. The initial connector should be tool-only. A custom embedded UI is not required for v1.

This is the right first path because:

- it fits the AI-native workflow described in the core documents
- it avoids building a separate human dashboard first
- it keeps the integration close to the MCP-oriented product vision
- it allows fast prompt-and-tool iteration inside real ChatGPT workflows

### DeepSeek As Fallback

If ChatGPT connector integration proves slower or more restrictive than expected, the fallback path is not to redesign the service. The fallback is to expose the same core capabilities through the direct HTTP API and, if needed, a thinner non-ChatGPT-specific adapter for another model client such as DeepSeek.

The core service remains the same in either case.

## Authentication Direction

V1 will use static access tokens.

There are two relevant surfaces:

- the direct service API
- the ChatGPT-facing MCP endpoint

For the direct service API, standard bearer-token access is appropriate.

For the ChatGPT MCP connector, v1 should avoid OAuth complexity and instead use a deployment-issued static token strategy. The default implementation should be:

- a tokenized connector URL issued to the customer

If later testing shows that connector-managed header injection is cleanly supported for the chosen workflow, that can be adopted as an equivalent transport detail. The design decision that matters is that v1 does not depend on per-user OAuth. Access control is intentionally simple and based on possession of a valid issued token.

## Architectural Overview

The system should be built as one codebase with clear internal boundaries and one deployment per corpus.

At a high level:

```text
Source Dataset
  -> Ingestion + Validation
  -> Processing + Enrichment
  -> Version Publication
  -> Serving Store
  -> Query Service
     -> MCP Adapter for ChatGPT
     -> Direct HTTP API for fallback clients and testing
  -> Evaluation Harness
```

Each deployment serves one global corpus. A second ecosystem or dataset should normally mean a second deployment with the same system shape, not a new tenant inside the first deployment.

## Major Components

### 1. Dataset Ingestion Layer

This layer receives raw synchronized data from external acquisition or sync workflows.

Responsibilities:

- accept new dataset drops
- validate basic structural integrity
- attach version identifiers and publication metadata
- store raw snapshots for traceability and rollback

This layer should be operationally simple. It does not need streaming, event sourcing, or low-latency ingestion.

### 2. Processing And Enrichment Layer

This layer turns raw inputs into a usable intelligence corpus.

Responsibilities:

- normalize source records into canonical structures
- derive comparative and aggregate metrics
- build category and ecosystem views
- generate metadata that improves exploration
- prepare raw evidence views for later inspection
- publish processed outputs for a specific dataset version

This layer is where the product becomes differentiated. The value is not in simply storing the source data. The value is in creating reliable analytical projections that an AI can use well.

### 3. Version Registry

This layer tracks dataset evolution over time.

Responsibilities:

- register each raw and processed dataset version
- identify the active serving version
- support rollback to an earlier version
- expose version metadata to the service and evaluation workflows

Versioning is a first-class concern in this design. Freshness is not strict, but repeatability and traceability matter.

### 4. Serving Store

The serving store holds the published corpus in a form optimized for the online workload.

The v1 workload is not pure key-value lookup. It includes:

- filtered exploration
- counts and distributions
- category slicing
- related-entity discovery
- ranking and comparison
- raw evidence retrieval

For that reason, the serving store should be shaped around analytical access patterns rather than simple document retrieval.

### 5. Query Service

This is the core product service.

Responsibilities:

- resolve the active or requested dataset version
- execute exploration and analytical queries
- retrieve raw evidence for verification and drill-down
- shape outputs into compact structured responses
- apply token-based access control
- expose stable service contracts to adapters and test harnesses

This layer must contain the business logic. Adapters should not contain analytical logic.

### 6. MCP Adapter

This is the ChatGPT-facing integration layer.

Responsibilities:

- expose the service as an MCP-compatible tool surface
- advertise tool metadata that helps model discovery and invocation
- translate MCP tool calls into internal query-service requests
- return structured tool results in a ChatGPT-friendly format

The adapter should use Streamable HTTP as the default transport unless implementation constraints force a different MCP transport.

This adapter should remain thin. It is a transport and tool-shaping layer, not the product core.

### 7. Evaluation Harness

This layer exists because answer quality is the main success metric.

Responsibilities:

- store benchmark analyst questions
- run the same question sets against baseline and corpus-enabled workflows
- compare outputs across dataset versions
- support manual and rubric-based scoring

Without this layer, the project risks building infrastructure without proving that the corpus materially improves answers.

## Storage And Infrastructure Direction

### Deployment Target

AWS is an appropriate default deployment target for v1.

The service should be designed so that each deployment corresponds to one corpus or ecosystem. This fits the requirement that one deployment may serve Shopify intelligence while another deployment may later serve a different intelligence corpus.

### Recommended V1 Stack Shape

The most pragmatic v1 infrastructure shape is:

- object storage for raw and processed dataset snapshots
- a relational or analytical online store for published serving views
- a long-running HTTP service for the query layer
- a thin MCP endpoint hosted alongside or in front of that service

### Preferred Storage Pattern

For v1, prefer:

- versioned object storage for raw artifacts and processed exports
- a relational serving store with materialized analytical views

This is preferable to a DynamoDB-first design for the initial implementation.

Why:

- the core workloads are analytical and slice-oriented, not simple key-based access
- the system needs ranking, filtering, grouping, comparison, and drill-down
- versioned publication is easier to reason about with snapshot-oriented processing and structured serving views

DynamoDB may still have a role later for specialized lookup-heavy components, but it should not be the primary modeling choice for this first build.

### Concrete Pragmatic Choice

If we want a concrete starting point, the best v1 implementation direction is:

- raw and processed versioned artifacts in S3
- preprocessing jobs that use a columnar local engine such as DuckDB during batch compilation
- a PostgreSQL-compatible serving database for published online views
- a containerized query service behind a stable HTTPS endpoint

This keeps the system simple, explainable, and easy to iterate on before introducing more specialized infrastructure.

## Compute And Service Shape

### Batch Compute

The processing pipeline should run as explicit batch jobs, not inline inside the serving path.

Suitable v1 options include:

- containerized batch jobs
- scheduled workers
- manually triggered publish jobs during early development

The important constraint is clear version publication, not automation sophistication.

### Online Compute

The query service should run as a long-lived HTTP service rather than a pile of disconnected functions.

Why:

- the service needs stable query behavior and controlled response shaping
- the system may reuse cached metadata and warm database connections
- MCP and direct HTTP access can both be served more cleanly from one running service boundary

An API Gateway plus stateless function design is possible, but it should not be the default v1 choice. A containerized service is a better fit for the current workload and integration model.

## Interface Model

The core service should be analytics-tool-oriented.

It should not expose:

- a generic search box as the primary product primitive
- a raw table browser as the main interface
- a generalized SQL-like interface for the model

Instead, it should expose a small family of analytical capabilities that match the real user questions.

### Tool Families

The v1 tool surface should center on five families:

1. Discovery tools
   Find relevant entities, cohorts, categories, and neighboring regions of the corpus.

2. Aggregate tools
   Return counts, rankings, distributions, trends, and comparisons.

3. Metadata exploration tools
   Help the model understand what dimensions and slices exist in the dataset.

4. Drill-down tools
   Inspect a specific entity or narrowed subset after discovery identifies a target.

5. Raw evidence tools
   Return the underlying supporting records or extracts needed to verify claims.

This tool model directly supports the intended analyst workflow of explore first, then investigate.

## Output Design Direction

Tool results should be structured, compact, and evidence-oriented.

Each response should aim to include:

- the analytical result itself
- enough metadata to interpret it correctly
- version information or provenance when relevant
- stable identifiers or handles for follow-up drill-down

The service should avoid dumping oversized raw records into the model context unless the user is specifically drilling into evidence.

## Raw And Processed Data Surfaces

The system must support both analytical and raw data access, but these should be treated as distinct surfaces over the same corpus.

### Analytical Surface

Purpose:

- trend discovery
- ecosystem understanding
- category analysis
- comparator discovery

Characteristics:

- shaped for LLM reasoning
- aggregate-heavy
- compact and metadata-rich

### Raw Evidence Surface

Purpose:

- verification
- supporting detail
- manual inspection
- export-oriented follow-up

Characteristics:

- closer to source records
- less summarized
- more suited to traceability than model-level reasoning

Both surfaces should be version-aware and backed by the same published corpus.

## Dataset Versioning Model

The service must treat dataset versions as explicit, query-visible state.

The default behavior should be to serve the active published version. However, the design should allow:

- pinning a request to a specific version
- comparing behavior across versions during evaluation
- rolling back a bad publication without changing the service architecture

This will matter both operationally and during answer-quality evaluation.

## ChatGPT Connector Design Direction

The ChatGPT integration should start with the simplest viable connector shape:

- private connector
- tool-only
- no embedded UI
- HTTPS `/mcp` endpoint
- developer-mode onboarding

This is intentionally not a polished public app submission flow. It is the fastest route to validating whether the corpus actually improves analyst outcomes in ChatGPT.

If later evidence shows that a richer UI materially improves workflows, an embedded MCP Apps UI can be added on top of the same core tools. It is not required for v1.

## Service Contract Strategy

The system should define one internal contract between the MCP adapter and the query service.

That contract should:

- be explicit
- be versioned
- avoid leaking storage-level structures
- be reusable by non-MCP clients

This allows the codebase to support:

- ChatGPT through MCP
- evaluation harnesses through direct HTTP
- future alternative adapters without rewriting business logic

## Repository Direction

The repository should evolve toward clear implementation boundaries.

Suggested top-level structure:

- `docs/` for architecture, product decisions, and evaluation notes
- `ingestion/` for raw dataset import and version registration
- `processing/` for normalization, enrichment, and publication jobs
- `service/` for the query service and analytical business logic
- `mcp/` for the ChatGPT-facing MCP adapter
- `evaluation/` for benchmark prompts, scoring notes, and quality comparisons
- `ops/` for deployment and publication scripts

This structure matches the actual system boundaries instead of organizing around client integrations alone.

## Implementation Phases

### Phase 1: Corpus Compilation

Build the ingestion, version registration, and processing pipeline.

Exit condition:

- one dataset version can be ingested, compiled, and published reproducibly

### Phase 2: Query Service

Build the core service and direct HTTP contracts for discovery, aggregation, metadata exploration, drill-down, and raw evidence retrieval.

Exit condition:

- the system can answer benchmark questions through the direct API

### Phase 3: ChatGPT Connector

Expose the query service through a private MCP connector and test it in ChatGPT developer mode.

Exit condition:

- a real analyst can run benchmark questions from ChatGPT with tool invocation working end to end

### Phase 4: Evaluation And Refinement

Compare baseline answers with corpus-enabled answers and improve the processing or tool design where the answer quality is weak.

Exit condition:

- the corpus-enabled workflow shows clear improvement on the benchmark set

## Major Risks

### 1. Analytical Surface Risk

The biggest design risk is still whether the chosen tools are the right abstraction for ecosystem exploration. If the tool surface is too narrow, the system will feel rigid. If it is too generic, the model will use it poorly.

### 2. Corpus Quality Risk

The service cannot compensate for a weak dataset. If the compiled corpus is incomplete, noisy, or poorly organized, answer quality will stall regardless of service quality.

### 3. Connector Friction Risk

ChatGPT connector onboarding and token handling may add practical friction. That is a deployment and user-experience risk, but it does not invalidate the core service design.

### 4. Over-Engineering Risk

Because the long-term vision suggests a generalized platform, there is a risk of building platform machinery too early. The implementation should stay disciplined and corpus-specific until answer quality is proven.

## Final Design Position

The v1 build should be a ChatGPT-first hosted intelligence service with:

- one deployment per intelligence corpus
- batch compilation into versioned published views
- an analytical query service as the real product core
- a thin MCP connector for ChatGPT
- simple token-gated access
- a direct evaluation loop focused on answer quality

The most important practical decision in this HLD is that we are not building a generic data platform first. We are building a specific analytical intelligence service, with ChatGPT as the primary client, and a service core designed to stay useful even if we later add other AI frontends.

## Reference Basis

The ChatGPT integration direction in this HLD is consistent with the current OpenAI documentation:

- Apps SDK quickstart states that ChatGPT apps use MCP, require an MCP server, and can be tools-only without a custom UI.
- Apps SDK MCP documentation recommends Streamable HTTP and describes the MCP server as the tool and metadata surface used by ChatGPT.
- Apps SDK connector documentation describes private testing through developer mode and connector creation against an HTTPS MCP endpoint.
- Apps SDK authentication documentation indicates that read-only anonymous operation is possible while authenticated MCP servers are expected to use OAuth 2.1, which is why v1 keeps authentication deliberately simple.
