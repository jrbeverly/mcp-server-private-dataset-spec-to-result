# Private Connector Onboarding — ChatGPT Developer Mode

**How to connect ChatGPT to a private MCP endpoint for testing and validation.**

This guide covers the developer-mode flow for exposing the corpus intelligence service to ChatGPT through a private MCP connector. It assumes the query service and MCP adapter are already deployed and healthy.

---

## Quick Overview

1. Deploy the service (or run it locally with a tunnel)
2. Configure a private connector URL in ChatGPT developer mode, embedding the static token
3. Invoke tool-assisted queries inside ChatGPT
4. Validate that discovery, aggregate, and drill-down operations return structured results

---

## Step 1 — Deploy the MCP Adapter

The MCP adapter must be reachable at a public HTTPS URL. For developer-mode testing, either:

**Option A — Cloud deployment (recommended for real testing)**

Deploy via the Terraform configuration in `ops/`. The adapter exposes two endpoints:

| Path | Method | Auth | Purpose |
|------|--------|------|---------|
| `/mcp` | POST | Token required | MCP JSON-RPC tool surface |
| `/.well-known/mcp` | GET | None | Server metadata (public) |

**Option B — Local tunnel**

Run both services locally and expose the MCP adapter through a tunnel:

```bash
# Terminal 1: Query Service
export AUTH_TOKEN=your-issued-token
export CORPUS_ID=example-corpus
export CORPUS_DEPLOYMENT_NAME=dev
export STORAGE_POSTGRES_CONNECTION_STRING="Host=localhost;Database=corpus;Username=postgres;Password=postgres"
export STORAGE_S3_BUCKET_NAME=dev-artifacts

cd service/QueryService/src/QueryService.Api
dotnet run
# Listening on http://localhost:5000

# Terminal 2: MCP Adapter
export AUTH_TOKEN=your-issued-token
export CORPUS_ID=example-corpus
export CORPUS_DEPLOYMENT_NAME=dev
export QUERY_SERVICE_URL=http://localhost:5000

cd mcp/McpAdapter/src
dotnet run
# Listening on http://localhost:5001
```

Then expose port 5001 through a tunneling tool:

```bash
# Example with ngrok
ngrok http 5001
# Use the resulting https://<id>.ngrok.io as the connector origin
```

---

## Step 2 — Get Your Tokenized Connector URL

The v1 access model uses a static bearer token. The token travels in one of two equivalent forms:

**Form A — Tokenized URL (preferred for ChatGPT dev mode)**

```
https://<adapter-host>/mcp?token=<your-issued-token>
```

ChatGPT appends this token as a query parameter. The MCP adapter's middleware extracts and validates it against the `AUTH_TOKEN` environment variable.

**Form B — Header injection**

If the connector configuration supports custom headers, set:

```
Header: X-Api-Token
Value: <your-issued-token>
```

Both forms are equivalent in the v1 flow. Use whichever the development environment supports most cleanly.

---

## Step 3 — Configure the Private Connector in ChatGPT

1. Open ChatGPT and navigate to **Settings → Developer** (or the Apps management panel).
2. Choose **Create custom connector** or **Add private connector**.
3. Enter the connector details:
   - **Name:** Corpus Intelligence (or your corpus name)
   - **MCP Endpoint URL:** `https://<adapter-host>/mcp?token=<your-issued-token>`
   - **Description:** "Private corpus for structured intelligence queries"
4. Save the connector. ChatGPT will call the endpoint's `initialize` and `tools/list` methods to discover the available tool surface.
5. If the connection succeeds, the five tools (`discover`, `aggregate`, `explore_metadata`, `drill_down`, `get_evidence`) appear as available tools in the developer panel.

---

## Step 4 — Test Representative Operations

Once the connector is registered, test the three core flows inside a ChatGPT conversation:

### Discovery

Ask ChatGPT:

> Search the dataset for entities related to cloud infrastructure and show me the top 5 matches.

ChatGPT should call `discover` with a query like `"cloud infrastructure"` and present the results. You can also test category-filtered searches and pagination.

### Aggregation

Ask ChatGPT:

> Show me the distribution of entities across categories, sorted by counts descending.

ChatGPT should call `aggregate` with `metric: "count"` and `groupBy: "category"`. Verify the response includes bucket counts and percentages.

### Drill-down

After discovery returns entity IDs, ask:

> Give me the full details for the top-ranked entity.

ChatGPT should call `drill_down` with the `entityId` from the discovery result. Verify that properties, metrics, and related entities are returned.

---

## Step 5 — Validate the Integration

**Success criteria for developer-mode testing:**

- [ ] `tools/list` returns five tools with correct names, descriptions, and input schemas
- [ ] Discovery returns relevance-ranked results with entity IDs
- [ ] Aggregation returns category distributions with counts and percentages
- [ ] Drill-down returns full properties, metrics, and related entities for a given ID
- [ ] Metadata exploration returns available dimensions and their value distributions
- [ ] Evidence retrieval returns paginated supporting records
- [ ] Requests without a valid token receive a 401 response (on paths other than `/.well-known`)
- [ ] The `/.well-known/mcp` endpoint is reachable without a token

---

## Troubleshooting

### Connector fails to register / tools/list returns an error

1. Verify the MCP adapter is running: `curl https://<adapter-host>/.well-known/mcp`
2. Check that the token in the URL matches `AUTH_TOKEN`
3. Inspect the adapter logs for authentication or connection errors

### Tool calls return empty results

1. Confirm a dataset version has been loaded and activated (check the query service logs)
2. Verify the query service is reachable from the adapter: check `QUERY_SERVICE_URL`
3. Test the query service directly: `curl -X POST http://localhost:5000/api/v1/discovery -H "X-Api-Token: <token>" -H "Content-Type: application/json" -d '{}'`

### Token rejected (401)

1. Confirm the `AUTH_TOKEN` environment variable is set identically on both the query service and the MCP adapter
2. If using the tokenized URL form, confirm the `?token=<value>` query parameter is present
3. Check that the token middleware is applied to the `/mcp` route (it is, by default, in `Program.cs`)

### Timeout or connection refused

1. Verify the adapter host is publicly reachable (not behind a firewall)
2. Check the adapter's health: `curl https://<adapter-host>/.well-known/mcp`
3. For local tunnel setups, restart the tunnel and update the connector URL
