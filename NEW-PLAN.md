# Cloud-Native GraphRAG Migration Plan

## Overview

Migrate CompoundDocs MCP server from a local-first (PostgreSQL+pgvector+Ollama, stdio transport) architecture to a cloud-native, team-shared GraphRAG system (Neptune+OpenSearch+Bedrock, HTTP transport on EKS).

**Key architectural decisions (confirmed):**
- Single shared knowledge graph across all repos (no tenant isolation on queries)
- API key authentication per developer
- Git clone + poll for change detection (default 10min interval)
- Agentic multi-step GraphRAG queries (2-4 LLM-guided traversal steps)
- Cross-repo entity resolution
- Markdown (with YAML frontmatter) only
- Keep and adapt existing skills/agents/hooks
- Amazon Titan V2 for embeddings, Claude Sonnet 4.5 (primary), Haiku 4.5 (entity extraction), Opus 4.5 (complex queries)

> **⛔ SAFETY NOTICE — CODE ONLY ⛔**
>
> **This plan produces code artifacts only.** No infrastructure will be created, no `tofu apply`, no `helm install`, no AWS API calls. Agents executing this plan must **never** provision AWS resources (Neptune, OpenSearch, Bedrock, EKS, IAM roles, VPCs, etc.) or run any deployment commands. All infrastructure and deployment definitions are **reference files** for a human operator to review and apply manually.

---

## 1. New AWS Infrastructure

| Service | Purpose | Config |
|---------|---------|--------|
| **Neptune Database Serverless** | Property graph (openCypher) | 1-16 NCU, same VPC as EKS |
| **OpenSearch Serverless** | Vector search (k-NN) | 1024-dim index, Titan V2 compatible |
| **Bedrock** | LLM + Embeddings | Claude Sonnet/Haiku/Opus 4.5, Titan Embed V2 |
| **EKS** | Kubernetes hosting | 2+ AZ, IRSA for AWS auth |
| **GHCR** | Container registry (GitHub Container Registry) | MCP server + Worker images |

> **⛔ INFRASTRUCTURE SAFETY — DO NOT PROVISION ⛔**
>
> **Agents executing this plan must NOT create, modify, or provision any AWS resources.** This includes but is not limited to: Neptune clusters, OpenSearch collections, Bedrock model access, EKS clusters, IAM roles/policies, VPCs, subnets, security groups, or any other AWS infrastructure.
>
> **Assume all infrastructure already exists.** Write only the application code that interacts with these services. Do not run `aws` CLI commands, CloudFormation, Terraform/OpenTofu apply, `eksctl`, or any other infrastructure provisioning tool. Infrastructure-as-code files in this repo (OpenTofu, Crossplane) are **reference artifacts only** — a human operator will decide when and whether to apply them.

---

## 2. Project Restructuring

### Remove
- `CompoundDocs.Cleanup` (not needed)

### Keep & Modify
- `CompoundDocs.AppHost` (Aspire orchestration used locally for development purposes)
- `CompoundDocs.McpServer` - Major rewrite: HTTP transport, new tools, new DI
- `CompoundDocs.Common` - Keep parsing, add graph models, replace config
- `CompoundDocs.ServiceDefaults` - Update health checks

### Add
| Project | Purpose |
|---------|---------|
| `CompoundDocs.Graph` | Neptune openCypher client, graph repository, query builders |
| `CompoundDocs.Vector` | OpenSearch Serverless client, vector CRUD + search |
| `CompoundDocs.Bedrock` | Bedrock wrappers for Claude (Converse API) + Titan embeddings |
| `CompoundDocs.GitSync` | LibGit2Sharp git clone/pull/diff |
| `CompoundDocs.GraphRag` | Agentic pipeline, entity extraction, cross-repo resolution |
| `CompoundDocs.Worker` | CronJob console app: runs git sync → ingestion pipeline, then exits |


### Package Changes (`Directory.Packages.props`)

**Add:**
- `AWSSDK.Neptunedata` - Neptune openCypher HTTP API
- `AWSSDK.BedrockRuntime` - Claude + Titan via Converse API
- `AWSSDK.OpenSearchServerless` - Vector store
- `LibGit2Sharp` - Git operations
- `Gremlin.Net` - Backup Neptune client

**Remove:**
- `Microsoft.SemanticKernel.Connectors.Ollama`
- `Microsoft.SemanticKernel.Connectors.PgVector`
- `Npgsql`, `Npgsql.EntityFrameworkCore.PostgreSQL`
- `QuikGraph`
- `Microsoft.EntityFrameworkCore.InMemory` (test)

**Keep:**
- `ModelContextProtocol` (update to latest, need HTTP transport support)
- `Markdig`, `YamlDotNet`, `NJsonSchema` (parsing)
- `Polly` (resilience)
- `Serilog` (logging)
- `Microsoft.SemanticKernel` (may keep core for AI abstractions, drop connectors)
- `Aspire.*`, `CommunityToolkit.Aspire.Hosting.Ollama` (orchestration for local development)
- All testing packages

---

## 3. Graph Schema (Neptune, openCypher)

```
Nodes:
  :Document     {id, title, path, repo, branch, hash, doc_type, promotion_level, last_modified}
  :Section      {id, title, level, order, content_preview}
  :Chunk        {id, text, embedding_id, token_count}
  :Concept      {id, name, type, description}
  :CodeExample  {id, language, snippet}

Relationships:
  (:Document)-[:HAS_SECTION]->(:Section)
  (:Section)-[:HAS_SUBSECTION]->(:Section)
  (:Section)-[:HAS_CHUNK]->(:Chunk)
  (:Chunk)-[:MENTIONS]->(:Concept)
  (:Chunk)-[:CONTAINS_EXAMPLE]->(:CodeExample)
  (:Document)-[:DEPENDS_ON]->(:Document)
  (:Document)-[:SUPERSEDES]->(:Document)
  (:Concept)-[:RELATED_TO {weight}]->(:Concept)
```

The graph stores **document content and its relationships only**. Repository and branch metadata is tracked by the git sync service (config + runtime state), not in the graph. The graph makes no assumptions about what documents contain — node types like `:Concept` and `:CodeExample` are discovered dynamically by the LLM (Haiku via Bedrock) during entity extraction, not prescribed by a fixed schema.

---

## 4. Key Service Interfaces

### `INeptuneClient` (CompoundDocs.Graph)
Low-level openCypher query execution via `AWSSDK.Neptunedata`.

### `IGraphRepository` (CompoundDocs.Graph)
- Document/Section/Chunk CRUD with cascade
- Concept entity upsert with cross-repo dedup
- Traversal queries: get related concepts (N hops), get linked documents, get chunks by concept

### `IVectorStore` (CompoundDocs.Vector)
- Index/delete embeddings by chunk ID
- k-NN search with metadata filters (repo, doc_type, promotion_level)
- Batch operations for ingestion

### `IBedrockLlmService` (CompoundDocs.Bedrock)
- `GenerateAsync(systemPrompt, messages, tier)` - Converse API to Claude
- `ExtractEntitiesAsync(chunkText)` - Structured entity extraction via Haiku
- Model tier enum: Haiku (cheap routing), Sonnet (primary), Opus (complex)

### `IBedrockEmbeddingService` (CompoundDocs.Bedrock)
- `GenerateEmbeddingAsync(text)` / `GenerateEmbeddingsAsync(texts)` via Titan V2
- 1024 dimensions

### `IGitSyncService` (CompoundDocs.GitSync)
- Clone or update repo to local path
- Detect changes (diff between last known commit and HEAD)
- Read changed file contents filtered by monitored paths

### `IGraphRagPipeline` (CompoundDocs.GraphRag)
- Multi-step agentic query orchestrator (see Section 5)

### `IDocumentIngestionService` (CompoundDocs.GraphRag)
- Parse markdown -> chunk by section -> extract entities (Haiku) -> generate embeddings (Titan) -> write to graph + vector store

---

## 5. Agentic GraphRAG Query Pipeline

```
Step 1: INITIAL RETRIEVAL
  - Embed query via Titan V2
  - Vector search in OpenSearch -> top-K chunks
  - Fetch graph context: concepts mentioned by those chunks

Step 2: CONCEPT EXPANSION (Haiku decides)
  - LLM sees initial chunks + concept list + related concepts from graph
  - Decides which concepts to explore deeper
  - Fetch chunks mentioning those expanded concepts (1-2 hops)

Step 3: DOCUMENT TRAVERSAL (Haiku decides, optional)
  - LLM identifies DEPENDS_ON/SUPERSEDES relationships worth following
  - Fetch sections/chunks from linked documents across repos

Step 4: SYNTHESIS (Sonnet or Opus)
  - All gathered chunks + graph topology context -> final answer
  - Source attribution with traversal log
  - Opus used when Sonnet indicates low confidence or max depth reached
```

Each routing decision uses Haiku with a structured JSON prompt/response for speed and cost. The `GraphRagConfig.MaxTraversalSteps` (default 4) caps the loop.

---

## 6. Incremental Graph Update Pipeline

```
CompoundDocs.Worker (runs as K8s CronJob on configurable schedule, e.g. every 10min)
  For each repo on each poll interval:

  1. git fetch origin <branch>
  2. git diff <last_hash>..<HEAD> --name-status
  3. Filter by MonitoredPaths (e.g., "docs/**/*.md")

  For ADDED/MODIFIED files:
    4. Parse markdown + frontmatter (reuse MarkdownParser, FrontmatterParser)
    5. Validate against doc type schema (reuse SchemaValidator)
    6. Chunk by section boundaries (reuse existing chunking logic)
    7. Generate embeddings via Titan V2
    8. Extract entities via Haiku (concepts, APIs, code examples)
    9. Cross-repo entity resolution (cosine similarity on names, threshold 0.92)
    10. Upsert graph nodes + relationships in Neptune
    11. Upsert vectors in OpenSearch

  For DELETED files:
    12. Cascade delete: Document -> Sections -> Chunks + orphaned Concepts
    13. Delete vectors from OpenSearch by document ID

  14. Update last-known commit hash in git sync service state
```

Document hooks (`IDocumentHook`) fire during steps 4-11, preserving BeforeIndex/AfterIndex lifecycle. Events (`IDocumentEventPublisher`) publish Created/Updated/Deleted as before.

---

## 7. MCP Tool Migration

### Transport: `WithStdioServerTransport()` -> `WithHttpTransport()`
### Program.cs: `Host.CreateDefaultBuilder` -> `WebApplication.CreateBuilder`
### Auth: `ApiKeyAuthMiddleware` extracts key from header, resolves developer name

### Tool Mapping (all 9 names preserved)

| Tool Name | Current Behavior | New Behavior |
|-----------|-----------------|--------------|
| `activate_project` | Sets session TenantKey | Registers repo URL for monitoring |
| `index_document` | Local file -> pgvector | Graph ingestion pipeline (parse, entities, graph+vectors) |
| `semantic_search` | pgvector similarity | Hybrid: OpenSearch vector + Neptune graph enrichment |
| `rag_query` | Single-shot vector search + context return | **Agentic GraphRAG pipeline** (Section 5) |
| `list_doc_types` | Query pgvector metadata | `MATCH (d:Document) RETURN DISTINCT d.doc_type` |
| `delete_documents` | Delete from pgvector | Cascade delete in graph + vector cleanup |
| `update_promotion_level` | Update pgvector metadata | Update Document node property + OpenSearch metadata |
| `search_external_docs` | Web search | Keep structure, Bedrock backend |
| `rag_query_external` | External RAG | Keep structure, Bedrock backend |

### New Tools
| Tool Name | Purpose |
|-----------|---------|
| `explore_graph` | Navigate graph relationships from a starting node |
| `cross_repo_search` | Explicit cross-repo semantic search with repo attribution |

### Skills & Agents
All 16 YAML skills keep exact names and definitions. They reference MCP tool names which are preserved. `SkillLoader`, `SkillExecutor`, `SkillDefinition`, `AgentLoader`, `AgentDefinition` carry over unchanged.

### Session Context
`ISessionContext` (tenant-based) -> `IRequestContext` (per-HTTP-request, from API key). No tenant isolation. Optional `ActiveRepo` filter for scoped queries.

---

## 8. Configuration Model

Replace `CompoundDocsServerOptions` + `GlobalConfig`/`ProjectConfig` with single `CompoundDocsCloudConfig`:

```
CompoundDocs:
  Aws:
    Region: us-east-1
  Neptune:
    Endpoint: <cluster>.neptune.amazonaws.com
    Port: 8182
  OpenSearch:
    CollectionEndpoint: <endpoint>
    IndexName: compound-docs-vectors
  Bedrock:
    EmbeddingModelId: amazon.titan-embed-text-v2:0
    SonnetModelId: anthropic.claude-sonnet-4-5-v1:0
    HaikuModelId: anthropic.claude-haiku-4-5-v1:0
    OpusModelId: anthropic.claude-opus-4-5-v1:0
  Repositories:
    - Url: https://github.com/org/repo1.git
      Name: repo1
      Branch: main
      MonitoredPaths: ["docs/", "specs/"]
      PollIntervalMinutes: 10
  Auth:
    ApiKeys:
      abc123: "developer-name"
  GraphRag:
    MaxTraversalSteps: 4
    MaxChunksPerQuery: 20
    MinRelevanceScore: 0.3
    UseCrossRepoLinks: true
```

Loaded from `appsettings.json` + environment variables (`CompoundDocs__Neptune__Endpoint`).

---

## 9. Infrastructure as Code — OpenTofu (Reference Only)

> **⛔ These files are reference artifacts. Do NOT run `tofu apply` or any provisioning commands. ⛔**

OpenTofu definitions live in `infra/tofu/` and declaratively describe all AWS resources needed by the system. They exist in the repo so a human operator can review, customize, and apply them manually when ready.

### Resources Defined

| Resource | OpenTofu Module | Notes |
|----------|----------------|-------|
| **Neptune Serverless** | `infra/tofu/neptune.tf` | Cluster + subnet group, 1-16 NCU |
| **OpenSearch Serverless** | `infra/tofu/opensearch.tf` | Collection, encryption & network policies, 1024-dim k-NN |
| **Bedrock Model Access** | `infra/tofu/bedrock.tf` | Model access for Claude Sonnet/Haiku/Opus 4.5, Titan Embed V2 |
| **IAM Roles & Policies** | `infra/tofu/iam.tf` | IRSA roles for EKS pods, least-privilege policies |
| **VPC / Networking** | `infra/tofu/vpc.tf` | VPC, private subnets, security groups, VPC endpoints |
| **EKS Cluster** | `infra/tofu/eks.tf` | Cluster, managed node groups, OIDC provider |

### File Structure
```
infra/tofu/
  ├── main.tf           # Provider config, backend (S3)
  ├── variables.tf      # Input variables (region, tags, etc.)
  ├── outputs.tf        # Endpoint URLs, ARNs for Helm values
  ├── neptune.tf
  ├── opensearch.tf
  ├── bedrock.tf
  ├── iam.tf
  ├── vpc.tf
  └── eks.tf
```

---

## 10. Deployment (Helm on EKS)

The Helm chart is packaged and published to GitHub (OCI via GHCR, e.g. `oci://ghcr.io/<org>/charts/compound-docs`). Container images also live on GHCR.

### Two Workloads
1. **MCP Server** — `Deployment` (2+ replicas, HPA on CPU). Handles developer MCP requests via HTTP.
2. **Worker** — `CronJob` (schedule: `*/10 * * * *` default). Runs git sync → ingestion pipeline for all configured repos, then exits. Each invocation is a short-lived pod.

### Key K8s Resources
- `ServiceAccount` with IRSA annotation for Neptune/OpenSearch/Bedrock IAM
- `ConfigMap` for non-sensitive config
- `Secret` for API keys (from AWS Secrets Manager via CSI driver)
- `Service` (ClusterIP) for MCP server
- `Ingress` or `LoadBalancer` if external access needed
- `HorizontalPodAutoscaler` for MCP server
- `CronJob` for Worker (configurable schedule via Helm values)
- `PersistentVolumeClaim` or `emptyDir` for Worker git clone storage

### Crossplane Infrastructure CRDs

The Helm chart optionally includes Crossplane compositions that can self-provision infrastructure when installed on a Crossplane-enabled cluster. These mirror the same resources defined in the OpenTofu files.

> **⛔ The Helm chart and Crossplane resources are code artifacts only. Running `helm install` is a human decision — agents must not execute it. ⛔**

**Crossplane Providers:**
- `provider-aws` — provisions Neptune, OpenSearch, IAM, VPC resources

**Compositions included in chart:**
| Composition | Resources Created |
|-------------|-------------------|
| `compound-docs-data` | Neptune Serverless cluster, OpenSearch Serverless collection |
| `compound-docs-network` | VPC, subnets, security groups, VPC endpoints |
| `compound-docs-iam` | IRSA roles, policies for pod-level AWS access |

**File Structure:**
```
deploy/helm/compound-docs/
  ├── Chart.yaml
  ├── values.yaml
  ├── templates/
  │   ├── server-deployment.yaml
  │   ├── worker-cronjob.yaml
  │   ├── service.yaml
  │   ├── hpa.yaml
  │   ├── configmap.yaml
  │   └── ...
  └── crossplane/          # Optional — ignored if Crossplane not installed
      ├── compositions/
      │   ├── data.yaml
      │   ├── network.yaml
      │   └── iam.yaml
      └── claims/
          └── compound-docs-infra.yaml
```

### Networking
- EKS and Neptune in same VPC, private subnets
- Security group allows TCP 8182 from EKS pod CIDR
- Neptune endpoint resolved via VPC DNS

---

## 11. Testing Strategy

| Level | Scope | Tools |
|-------|-------|-------|
| **Unit** | Graph query builders, entity extraction parsing, pipeline step logic, config loading | xUnit, Moq, Shouldly |
| **Integration** | Full ingestion pipeline, full query pipeline, git sync | Neptune local Docker, LocalStack (OpenSearch), mocked Bedrock |
| **E2E** | MCP tool calls over HTTP, multi-user concurrent access | Deployed test namespace in EKS |

Existing unit tests for `MarkdownParser`, `FrontmatterParser`, `SchemaValidator`, `ChunkingStrategy` are preserved and still pass.

---

## 12. Implementation Phases

### Phase 1: Foundation
- Create 6 new projects, update solution file
- Update `Directory.Packages.props` (add AWS SDKs, remove Ollama/PgVector; keep Aspire)
- Implement `CloudConfig` configuration model
- Implement `INeptuneClient` + `NeptuneClient`
- Implement `IBedrockEmbeddingService` + `IBedrockLlmService`
- Implement `IVectorStore` + `OpenSearchVectorStore`
- Unit tests for all new clients

### Phase 2: Graph Data Layer
- Define graph node/relationship models in `CompoundDocs.Common/Models/`
- Implement `GraphSchemaManager` (Neptune constraint + index creation)
- Implement `IGraphRepository` + `NeptuneGraphRepository`
- Build openCypher query templates
- Integration tests against Neptune local Docker

### Phase 3: Ingestion Pipeline
- Implement `EntityExtractor` (Haiku-based)
- Implement `IDocumentIngestionService`
- Port chunking logic from existing `ChunkingStrategy`
- Implement `CrossRepoEntityResolver`
- Adapt document hooks and event publisher
- Integration test: markdown file -> graph + vectors

### Phase 4: Git Sync + Worker
- Implement `IGitSyncService` with LibGit2Sharp
- Create `CompoundDocs.Worker` as a console app (run-once, exit on completion)
- Wire Worker: git sync → ingestion pipeline for all configured repos
- Integration test: simulated git push -> Worker run -> graph updated

### Phase 5: Agentic GraphRAG Pipeline
- Implement `IGraphRagPipeline` + `GraphRagPipeline`
- Implement LLM routing prompts (concept expansion, document traversal)
- Implement model tier selection logic
- Implement synthesis with source attribution
- Unit + integration tests for multi-step traversal

### Phase 6: MCP Server Migration
- Rewrite `Program.cs` (WebApplication + HTTP MCP)
- Implement `ApiKeyAuthMiddleware`
- Rewrite all 9 tools for new backend
- Add 2 new tools (`explore_graph`, `cross_repo_search`)
- Rewrite DI extension methods
- Port skills, agents, hooks (adapt implementations)
- Replace `ISessionContext` with `IRequestContext`

### Phase 7: Deployment
- Create Dockerfiles (MCP server + Worker)
- Create Helm chart
- Set up AWS infra (Neptune, OpenSearch, IAM)
- Create CI/CD pipeline (GitHub Actions -> GHCR for images + Helm chart OCI)
- Deploy to dev namespace

### Phase 8: Validation
- E2E tests against deployed stack
- Load testing (concurrent developers)
- Observability (OpenTelemetry, structured logging)
- Security review (API key rotation, IAM least-privilege, network policies)

---

## 13. Verification

After each phase, verify:
- `dotnet build` succeeds for entire solution
- `dotnet test` passes all unit + integration tests
- For Phase 6+: MCP tools respond correctly via HTTP (`curl` or test client)
- For Phase 7+: `helm install` succeeds, pods reach Ready state, health checks pass
- For Phase 8: Multiple developers can query simultaneously, git changes propagate within poll interval

### Critical Files to Modify
- `src/CompoundDocs.McpServer/McpServerBuilder.cs` - Central wiring (rewrite)
- `src/CompoundDocs.McpServer/Program.cs` - Entry point (rewrite)
- `src/CompoundDocs.McpServer/Tools/RagQueryTool.cs` - Most complex tool (rewrite)
- `src/CompoundDocs.McpServer/Data/Repositories/IDocumentRepository.cs` - Replaced by IGraphRepository + IVectorStore
- `src/CompoundDocs.McpServer/Services/FileWatcher/FileWatcherBackgroundService.cs` - Replaced by CompoundDocs.Worker CronJob
- `src/CompoundDocs.McpServer/Options/McpServerOptions.cs` - Replaced by CloudConfig
- `Directory.Packages.props` - Package additions/removals
