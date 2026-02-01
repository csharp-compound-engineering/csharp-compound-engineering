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

---

## 1. New AWS Infrastructure

| Service | Purpose | Config |
|---------|---------|--------|
| **Neptune Database Serverless** | Property graph (openCypher) | 1-16 NCU, same VPC as EKS |
| **OpenSearch Serverless** | Vector search (k-NN) | 1024-dim index, Titan V2 compatible |
| **Bedrock** | LLM + Embeddings | Claude Sonnet/Haiku/Opus 4.5, Titan Embed V2 |
| **EKS** | Kubernetes hosting | 2+ AZ, IRSA for AWS auth |
| **ECR** | Container registry | MCP server + Worker images |

---

## 2. Project Restructuring

### Remove
- `CompoundDocs.AppHost` (Aspire orchestrator replaced by K8s)
- `CompoundDocs.Cleanup` (reimplemented as graph maintenance job)

### Keep & Modify
- `CompoundDocs.McpServer` - Major rewrite: HTTP transport, new tools, new DI
- `CompoundDocs.Common` - Keep parsing, add graph models, replace config
- `CompoundDocs.ServiceDefaults` - Update health checks

### Add
| Project | Purpose |
|---------|---------|
| `CompoundDocs.Graph` | Neptune openCypher client, graph repository, query builders |
| `CompoundDocs.Vector` | OpenSearch Serverless client, vector CRUD + search |
| `CompoundDocs.Bedrock` | Bedrock wrappers for Claude (Converse API) + Titan embeddings |
| `CompoundDocs.GitSync` | LibGit2Sharp git clone/pull/diff, polling BackgroundService |
| `CompoundDocs.GraphRag` | Agentic pipeline, entity extraction, cross-repo resolution |
| `CompoundDocs.Worker` | Background host for git polling + graph maintenance |

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
- `Aspire.*`, `CommunityToolkit.Aspire.Hosting.Ollama`
- `Microsoft.EntityFrameworkCore.InMemory` (test)

**Keep:**
- `ModelContextProtocol` (update to latest, need HTTP transport support)
- `Markdig`, `YamlDotNet`, `NJsonSchema` (parsing)
- `Polly` (resilience)
- `Serilog` (logging)
- `Microsoft.SemanticKernel` (may keep core for AI abstractions, drop connectors)
- All testing packages

---

## 3. Graph Schema (Neptune, openCypher)

```
Nodes:
  :Repository   {id, url, name, monitored_paths[], poll_interval_min}
  :Branch       {id, repo_id, name, last_commit_hash, last_polled_at}
  :Document     {id, title, path, repo, branch, hash, doc_type, promotion_level, last_modified}
  :Section      {id, title, level, order, content_preview}
  :Chunk        {id, text, embedding_id, token_count}
  :Concept      {id, name, type, description}
  :API          {id, name, namespace, signature, return_type}
  :CodeExample  {id, language, snippet}

Relationships:
  (:Repository)-[:TRACKS_BRANCH]->(:Branch)
  (:Branch)-[:CONTAINS_DOC]->(:Document)
  (:Document)-[:HAS_SECTION]->(:Section)
  (:Section)-[:HAS_SUBSECTION]->(:Section)
  (:Section)-[:HAS_CHUNK]->(:Chunk)
  (:Chunk)-[:MENTIONS]->(:Concept)
  (:Chunk)-[:REFERENCES_API]->(:API)
  (:Chunk)-[:CONTAINS_EXAMPLE]->(:CodeExample)
  (:Document)-[:DEPENDS_ON]->(:Document)
  (:Document)-[:SUPERSEDES]->(:Document)
  (:Concept)-[:RELATED_TO {weight}]->(:Concept)
  (:API)-[:BELONGS_TO]->(:Concept)
  (:API)-[:IMPLEMENTS]->(:Concept)
```

---

## 4. Key Service Interfaces

### `INeptuneClient` (CompoundDocs.Graph)
Low-level openCypher query execution via `AWSSDK.Neptunedata`.

### `IGraphRepository` (CompoundDocs.Graph)
- Document/Section/Chunk CRUD with cascade
- Concept/API entity upsert with cross-repo dedup
- Traversal queries: get related concepts (N hops), get linked documents, get chunks by concept
- Repository/Branch management

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
GitPollerBackgroundService (runs in Worker process)
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

  14. Update Branch.last_commit_hash in graph
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

## 9. Deployment (Helm on EKS)

### Two Deployments
1. **MCP Server** (2+ replicas, HPA on CPU) - Handles developer MCP requests via HTTP
2. **Worker** (1 replica) - Runs git polling + graph maintenance

### Key K8s Resources
- `ServiceAccount` with IRSA annotation for Neptune/OpenSearch/Bedrock IAM
- `ConfigMap` for non-sensitive config
- `Secret` for API keys (from AWS Secrets Manager via CSI driver)
- `Service` (ClusterIP) for MCP server
- `Ingress` or `LoadBalancer` if external access needed
- `HorizontalPodAutoscaler` for MCP server
- `PersistentVolume` or `emptyDir` for Worker git clone storage

### Networking
- EKS and Neptune in same VPC, private subnets
- Security group allows TCP 8182 from EKS pod CIDR
- Neptune endpoint resolved via VPC DNS

---

## 10. Testing Strategy

| Level | Scope | Tools |
|-------|-------|-------|
| **Unit** | Graph query builders, entity extraction parsing, pipeline step logic, config loading | xUnit, Moq, Shouldly |
| **Integration** | Full ingestion pipeline, full query pipeline, git sync | Neptune local Docker, LocalStack (OpenSearch), mocked Bedrock |
| **E2E** | MCP tool calls over HTTP, multi-user concurrent access | Deployed test namespace in EKS |

Existing unit tests for `MarkdownParser`, `FrontmatterParser`, `SchemaValidator`, `ChunkingStrategy` are preserved and still pass.

---

## 11. Implementation Phases

### Phase 1: Foundation
- Create 6 new projects, update solution file
- Update `Directory.Packages.props` (add AWS SDKs, remove Ollama/PgVector/Aspire)
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

### Phase 4: Git Sync
- Implement `IGitSyncService` with LibGit2Sharp
- Implement `GitPollerBackgroundService`
- Create `CompoundDocs.Worker/Program.cs`
- Wire git poller -> ingestion pipeline
- Integration test: simulated git push -> graph updated

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
- Set up AWS infra (Neptune, OpenSearch, IAM, ECR)
- Create CI/CD pipeline (GitHub Actions -> ECR -> Helm)
- Deploy to dev namespace

### Phase 8: Validation
- E2E tests against deployed stack
- Load testing (concurrent developers)
- Observability (OpenTelemetry, structured logging)
- Security review (API key rotation, IAM least-privilege, network policies)

---

## 12. Verification

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
- `src/CompoundDocs.McpServer/Services/FileWatcher/FileWatcherBackgroundService.cs` - Template for GitPollerBackgroundService
- `src/CompoundDocs.McpServer/Options/McpServerOptions.cs` - Replaced by CloudConfig
- `Directory.Packages.props` - Package additions/removals
