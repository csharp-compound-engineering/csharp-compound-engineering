# Correction Plan: Single-Tool Knowledge Oracle

The implemented codebase has architectural errors from the original plan. This plan corrects the MCP server into a single-tool (`rag_query`) knowledge oracle with no tenant/project/repository concepts in the tool surface, no PostgreSQL, and emergent (LLM-discovered) doc types.

---

## Phase 1: Delete Obsolete Tools

Keep only `RagQueryTool.cs`, `ToolResponse.cs`, `ToolErrors.cs`. Delete everything else in `Tools/`:

**DELETE:**
- `src/CompoundDocs.McpServer/Tools/ActivateProjectTool.cs`
- `src/CompoundDocs.McpServer/Tools/IndexDocumentTool.cs`
- `src/CompoundDocs.McpServer/Tools/SemanticSearchTool.cs`
- `src/CompoundDocs.McpServer/Tools/ListDocTypesTool.cs`
- `src/CompoundDocs.McpServer/Tools/DeleteDocumentsTool.cs`
- `src/CompoundDocs.McpServer/Tools/UpdatePromotionLevelTool.cs`
- `src/CompoundDocs.McpServer/Tools/SearchExternalDocsTool.cs`
- `src/CompoundDocs.McpServer/Tools/RagQueryExternalTool.cs`
- `src/CompoundDocs.McpServer/Tools/DiagnosticsTool.cs`
- `src/CompoundDocs.McpServer/Tools/ConfigManagementTool.cs`

**MODIFY `ToolErrors.cs`:** Remove `NoActiveProject`, `InvalidDocType`, `InvalidPromotionLevel`, `OllamaUnavailable`, `DatabaseUnavailable`. Keep `EmptyQuery`, `SearchFailed`, `RagSynthesisFailed`, `EmbeddingFailed`, `OperationCancelled`, `UnexpectedError`.

---

## Phase 2: Delete PostgreSQL / Tenant / Session Infrastructure

**DELETE (Data layer):**
- `src/CompoundDocs.McpServer/Data/TenantDbContext.cs`
- `src/CompoundDocs.McpServer/Data/Configuration/BranchConfiguration.cs`
- `src/CompoundDocs.McpServer/Data/Configuration/RepoPathConfiguration.cs`
- `src/CompoundDocs.McpServer/Data/Entities/Branch.cs`
- `src/CompoundDocs.McpServer/Data/Entities/RepoPath.cs`
- `src/CompoundDocs.McpServer/Data/Repositories/` (entire directory — `BranchRepository`, `IBranchRepository`, `RepoPathRepository`, `IRepoPathRepository`, `DocumentRepository`, `IDocumentRepository`)

**DELETE (pgvector/Semantic Kernel):**
- `src/CompoundDocs.McpServer/SemanticKernel/VectorStoreFactory.cs`

**DELETE (Tenant/Session):**
- `src/CompoundDocs.McpServer/Session/SessionContext.cs`
- `src/CompoundDocs.McpServer/Session/ISessionContext.cs`
- `src/CompoundDocs.McpServer/Session/ProjectActivationService.cs`
- `src/CompoundDocs.McpServer/Session/TenantKeyProvider.cs` (if exists)
- `src/CompoundDocs.McpServer/Filters/TenantFilter.cs`

**DELETE (External docs — tools removed):**
- `src/CompoundDocs.McpServer/Models/ExternalDocument.cs`
- `src/CompoundDocs.McpServer/Models/ExternalDocumentChunk.cs`
- `src/CompoundDocs.McpServer/Services/ExternalDocs/` (entire directory)

---

## Phase 3: Delete Hardcoded DocType Infrastructure

Doc types are now emergent — discovered by Haiku via Bedrock during entity extraction, stored as properties in the graph.

**DELETE (entire `DocTypes/` directory):**
- `src/CompoundDocs.McpServer/DocTypes/DocTypeRegistry.cs`
- `src/CompoundDocs.McpServer/DocTypes/DocTypeValidator.cs`
- `src/CompoundDocs.McpServer/DocTypes/DocTypeDefinition.cs`
- `src/CompoundDocs.McpServer/DocTypes/DocTypeValidationResult.cs`
- `src/CompoundDocs.McpServer/DocTypes/IDocTypeRegistry.cs`

**DELETE (if exists):**
- `src/CompoundDocs.McpServer/Services/DocTypeRegistrationService.cs`

---

## Phase 4: Update DI, Builder, and Program.cs

**DELETE entirely:**
- `src/CompoundDocs.McpServer/DependencyInjection/DataServiceCollectionExtensions.cs` (PostgreSQL/EF Core registrations)
- `src/CompoundDocs.McpServer/DependencyInjection/SessionServiceCollectionExtensions.cs` (ISessionContext, ProjectActivationService)
- `src/CompoundDocs.McpServer/DependencyInjection/FileWatcherServiceCollectionExtensions.cs` (replaced by Worker CronJob)

**MODIFY `ToolServiceCollectionExtensions.cs`:**
- Remove all tool registrations except `RagQueryTool`
- Remove all `using` references to deleted tool types
- `AddMcpTools()` registers only RagQueryTool's dependencies (IVectorStore, IGraphRepository, IBedrockEmbeddingService, IBedrockLlmService)

**MODIFY `AdvancedServicesCollectionExtensions.cs`:**
- Remove DocType registration calls
- Remove any references to `DocumentLinkGraph`, `IDocTypeRegistry`
- Review each service for tenant/postgres dependencies and remove

**MODIFY `DocumentProcessingServiceCollectionExtensions.cs`:**
- Remove any registrations that depend on `IDocumentRepository` (pgvector)
- Keep `MarkdownParser`, `FrontmatterParser`, `SchemaValidator` if still used by ingestion pipeline
- Review `DocumentProcessor`, `DocumentIndexer` for pgvector dependencies

**MODIFY `McpServerBuilder.cs`:**
- Update `ConfigureServer()` to register only `RagQueryTool`
- Remove tool list comments referencing 9+ tools
- Remove session/project hooks if they exist

**MODIFY `Program.cs`:**
- Remove all PostgreSQL connection option parsing
- Remove `PostgresConnectionOptions` usage
- Remove Ollama config if all embeddings go through Bedrock
- Wire up AWS config (Neptune, OpenSearch, Bedrock) from `CompoundDocsCloudConfig`

---

## Phase 5: Update Models

**MODIFY `src/CompoundDocs.Common/Models/GraphModels.cs`:**
- Remove `Repository` property from `DocumentNode` (graph doesn't track repos)
- `DocType` remains as `string?` — emergent, set by LLM

**MODIFY `src/CompoundDocs.McpServer/Models/CompoundDocument.cs`:**
- Remove `TenantKey` property and `CreateTenantKey()`/`ParseTenantKey()` methods
- Remove all `[VectorStoreKey]`, `[VectorStoreData]`, `[VectorStoreVector]` pgvector annotations
- Remove `DocumentTypes` static class (hardcoded types)
- Simplify to a POCO for graph/vector storage

**MODIFY `src/CompoundDocs.McpServer/Models/DocumentChunk.cs`:**
- Remove pgvector annotations and `TenantKey`

**DELETE `src/CompoundDocs.Common/Graph/DocumentLinkGraph.cs`:**
- Uses QuikGraph, duplicates what Neptune provides. Link traversal goes through `IGraphRepository`.

---

## Phase 6: Update Options & Configuration

**MODIFY `src/CompoundDocs.McpServer/Options/McpServerOptions.cs`:**
- Remove `PostgresConnectionOptions` class entirely
- Remove `Postgres` property from `CompoundDocsServerOptions`
- Remove `OllamaConnectionOptions` if switching fully to Bedrock

---

## Phase 7: Rewrite RagQueryTool

**MODIFY `src/CompoundDocs.McpServer/Tools/RagQueryTool.cs`:**
- Remove dependency on `IDocumentRepository` (pgvector) → use `IVectorStore` from `CompoundDocs.Vector`
- Remove dependency on `ISessionContext` → no project/tenant concept
- Remove `_sessionContext.IsProjectActive` check
- Remove `_sessionContext.TenantKey` usage
- New flow: embed query (IBedrockEmbeddingService) → search OpenSearch (IVectorStore) → enrich with graph (IGraphRepository) → synthesize (IBedrockLlmService)
- Constructor injects: `IVectorStore`, `IGraphRepository`, `IBedrockEmbeddingService`, `IBedrockLlmService`, `IGraphRagPipeline`

---

## Phase 8: Update Package References

**MODIFY `Directory.Packages.props` — REMOVE:**
- `Microsoft.SemanticKernel.Connectors.PgVector`
- `Npgsql`
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `QuikGraph`
- `Microsoft.EntityFrameworkCore.InMemory`
- `Aspire.Hosting.PostgreSQL` (no PostgreSQL dependency remains)

**MODIFY `src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj`:**
- Remove: `Microsoft.SemanticKernel.Connectors.PgVector`, `Npgsql`, `Npgsql.EntityFrameworkCore.PostgreSQL`
- Add ProjectReferences: `CompoundDocs.Vector`, `CompoundDocs.Graph`, `CompoundDocs.Bedrock`

**MODIFY `src/CompoundDocs.Common/CompoundDocs.Common.csproj`:**
- Remove `QuikGraph` PackageReference

---

## Phase 9: Delete `docker/` Directory

The entire PostgreSQL Docker setup is obsolete.

**DELETE entire directory:**
- `docker/postgres/Dockerfile`
- `docker/postgres/init-db.sh`
- `docker/postgres/changelog/` (all Liquibase changelogs: 001-create-schema, 002-repo-paths-table, 003-branches-table, 004-create-indexes)

---

## Phase 10: Rewrite Skills (Wrong Format)

The existing skills use a completely wrong format. Claude Code skills are `SKILL.md` files in `.claude/skills/<name>/SKILL.md` directories with YAML frontmatter — NOT `.yaml` files with custom schemas.

### Current format (WRONG):
```
skills/cdocs-query.yaml          # Wrong location, wrong format, wrong extension
skills/cdocs-help.yaml           # Custom YAML schema with triggers, parameters, tool_calls, output templates
```

### Correct format:
```
.claude/skills/<name>/SKILL.md   # YAML frontmatter + markdown body
```

### Frontmatter fields (actual Claude Code schema):
- `name` — slash command name (e.g. `cdocs-query`)
- `description` — what the skill does and WHEN to use it (critical for auto-discovery)
- `allowed-tools` — tools Claude can use without permission (e.g. `mcp__compound-docs__rag_query`)
- `argument-hint` — autocomplete hint (e.g. `[question]`)
- `model` — `sonnet`, `opus`, `haiku`, or `inherit`
- `context` — set to `fork` for isolated subagent execution
- `agent` — subagent type when `context: fork`
- `user-invocable` — `true`/`false`
- `disable-model-invocation` — `true`/`false`
- `hooks` — lifecycle hooks scoped to this skill

### Step 1: DELETE entire `skills/` directory (all 17 .yaml files)

### Step 2: DELETE `plugins/csharp-compounding-docs/skills/` directory (all schema.yaml files)

### Step 3: CREATE new skills in correct format

**`.claude/skills/cdocs-query/SKILL.md`:**
```yaml
---
name: cdocs-query
description: Query the project knowledge base for documentation, patterns, and technical information. Use when the user asks about project architecture, conventions, documented decisions, or needs factual information from the knowledge base.
argument-hint: "[question]"
allowed-tools: mcp__compound-docs__rag_query
---

# Knowledge Base Query

Query the knowledge base using the `mcp__compound-docs__rag_query` tool.

## When to use
- User asks about project architecture, patterns, or conventions
- User needs documented technical information
- User references knowledge base content
- User asks "how do we..." or "what is our approach to..." questions

## Query patterns
- For specific topics: use precise, focused queries
- For broad questions: break into multiple focused queries
- Always cite the sources returned by the tool
- If no results found, state that clearly rather than fabricating answers
```

**`.claude/skills/cdocs-help/SKILL.md`:**
```yaml
---
name: cdocs-help
description: Show help for the CompoundDocs knowledge base system. Use when the user asks about available cdocs commands or how to use the knowledge base.
disable-model-invocation: true
---

# CompoundDocs Help

## Available Skills

| Skill | Description |
|-------|-------------|
| `/cdocs-query [question]` | Query the knowledge base for project documentation and patterns |

## Usage

Ask any question about the project and `/cdocs-query` will search the knowledge base:

```
/cdocs-query How is authentication configured?
```

The knowledge base is automatically populated by the background worker from monitored git repositories.
```

---

## Phase 11: Rewrite Agents (Wrong Format)

The existing agents use a completely wrong format. Claude Code agents are standalone `.md` files in `.claude/agents/<name>.md` with YAML frontmatter — NOT `.yaml` files with custom schemas.

### Current format (WRONG):
```
agents/best-practices-researcher.yaml   # Custom YAML with mcp_tools, skills, prompts, configuration
agents/framework-docs-researcher.yaml   # References deleted tools (semantic_search, list_doc_types, rag_query_external)
agents/repo-research-analyst.yaml       # References deleted tools
agents/git-history-analyzer.yaml        # References deleted tools
```

### Correct format:
```
.claude/agents/<name>.md   # YAML frontmatter + markdown system prompt
```

### Frontmatter fields (actual Claude Code schema):
- `name` — unique identifier
- `description` — when Claude should delegate to this agent (critical)
- `tools` — comma-separated allowlist (e.g. `Read, Grep, Glob, Bash`)
- `disallowedTools` — comma-separated denylist
- `model` — `sonnet`, `opus`, `haiku`, or `inherit`
- `permissionMode` — `default`, `acceptEdits`, `dontAsk`, `bypassPermissions`, `plan`
- `skills` — list of skill names to preload
- `hooks` — lifecycle hooks scoped to this agent

### Step 1: DELETE entire `agents/` directory (all 4 .yaml files)

### Step 2: DELETE agent infrastructure code that loaded the old format:
- `src/CompoundDocs.McpServer/Agents/AgentDefinition.cs`
- `src/CompoundDocs.McpServer/Agents/AgentLoader.cs`
- `src/CompoundDocs.McpServer/Agents/AgentRegistry.cs`
- `src/CompoundDocs.McpServer/DependencyInjection/AgentServiceCollectionExtensions.cs`
- `tests/CompoundDocs.Tests/Agents/AgentLoaderTests.cs`
- Any agent schema JSON files

### Step 3: CREATE new agents in correct format

**`.claude/agents/best-practices-researcher.md`:**
```yaml
---
name: best-practices-researcher
description: Research best practices for technologies and patterns. Use when the user needs authoritative guidance on implementation approaches, official recommendations, or industry best practices.
tools: Read, Grep, Glob, Bash, WebSearch, WebFetch, mcp__context7__resolve-library-id, mcp__context7__query-docs, mcp__compound-docs__rag_query
model: sonnet
skills:
  - cdocs-query
---

You are a Best Practices Researcher. Your role is to find and synthesize
best practices from multiple authoritative sources.

When researching:
1. Query Context7 for official framework documentation
2. Query the project knowledge base for team-specific patterns
3. Cross-reference findings across sources
4. Cite sources and provide confidence levels

Always provide:
- Clear recommendation with rationale
- Source citations
- Alternative approaches when applicable
```

**`.claude/agents/framework-docs-researcher.md`:**
```yaml
---
name: framework-docs-researcher
description: Find API documentation, code examples, and framework-specific guidance. Use when the user needs detailed API references, migration guides, or framework configuration help.
tools: Read, Grep, Glob, Bash, WebSearch, WebFetch, mcp__context7__resolve-library-id, mcp__context7__query-docs, mcp__compound-docs__rag_query
model: sonnet
skills:
  - cdocs-query
---

You are a Framework Documentation Researcher. Your role is to find detailed
API documentation and code examples.

When researching:
1. Use Context7 for up-to-date framework docs
2. Query the project knowledge base for local patterns
3. Always specify framework versions
4. Include complete, runnable code examples

Output format:
- API signature with parameters and return types
- Brief description
- Code example with comments
- Common pitfalls
- Related APIs or alternatives
```

**`.claude/agents/repo-research-analyst.md`:**
```yaml
---
name: repo-research-analyst
description: Analyze repository structure, identify patterns, and map codebase architecture. Use when the user needs to understand code organization, find implementations, or analyze dependencies.
tools: Read, Grep, Glob, Bash, mcp__compound-docs__rag_query
model: sonnet
skills:
  - cdocs-query
---

You are a Repository Research Analyst. Your role is to understand and
document codebase structure and architectural patterns.

When analyzing:
1. Start with high-level structure before diving into details
2. Look for standard patterns (Clean Architecture, DDD, etc.)
3. Map relationships between components
4. Cross-reference with project documentation

Output format:
- Visual representation when helpful (ASCII diagrams)
- Hierarchical structure with explanations
- Pattern identification with examples
```

**`.claude/agents/git-history-analyzer.md`:**
```yaml
---
name: git-history-analyzer
description: Analyze git history to find patterns, related changes, and identify subject matter experts. Use when the user needs to understand code evolution, find related commits, or identify who knows about specific areas.
tools: Read, Grep, Glob, Bash, mcp__compound-docs__rag_query
model: sonnet
skills:
  - cdocs-query
---

You are a Git History Analyzer. Your role is to mine repository history
for insights about code evolution and expertise.

When analyzing:
1. Use git log, blame, and diff for history
2. Cross-reference with project documentation
3. Identify patterns in change sets
4. Note significant milestones

Output format:
- Summary with key commits
- Timeline of significant changes
- Author/expertise analysis when relevant
```

---

## Phase 12: Delete Skill/Agent Infrastructure Code

The old custom YAML skill/agent loading infrastructure must be removed. Claude Code handles skill/agent loading natively from `.claude/skills/` and `.claude/agents/`.

**DELETE (Agents infrastructure — entire `Agents/` directory):**
- `src/CompoundDocs.McpServer/Agents/AgentDefinition.cs`
- `src/CompoundDocs.McpServer/Agents/AgentLoader.cs`
- `src/CompoundDocs.McpServer/Agents/AgentRegistry.cs`
- `src/CompoundDocs.McpServer/Agents/IAgentRegistry.cs`
- `src/CompoundDocs.McpServer/Agents/AgentInitializationService.cs`

**DELETE (Skills infrastructure — entire `Skills/` directory):**
- `src/CompoundDocs.McpServer/Skills/SkillDefinition.cs`
- `src/CompoundDocs.McpServer/Skills/SkillLoader.cs`
- `src/CompoundDocs.McpServer/Skills/SkillExecutor.cs`
- `src/CompoundDocs.McpServer/Skills/ISkillExecutor.cs`
- `src/CompoundDocs.McpServer/Skills/Capture/` (ICaptureSkillHandler, CaptureSkillHandler, CaptureRequest, CaptureResult, FrontmatterGenerator, ContentTemplates)
- `src/CompoundDocs.McpServer/Skills/Query/` (IQuerySkillHandler, QuerySkillHandler, QueryRequest, QueryResult)
- `src/CompoundDocs.McpServer/Skills/Meta/` (IMetaSkillHandler, MetaSkillHandler)
- `src/CompoundDocs.McpServer/Skills/Utility/` (IUtilitySkillHandler, UtilitySkillHandler)

**DELETE (DI wiring):**
- `src/CompoundDocs.McpServer/DependencyInjection/AgentServiceCollectionExtensions.cs`

**DELETE (Schema JSON files):**
- `skills/skill-schema.json`
- `agents/agent-schema.json`

**DELETE (Doc type schemas — hardcoded types removed):**
- `schemas/problem.schema.json`
- `schemas/insight.schema.json`
- `schemas/codebase.schema.json`
- `schemas/tool.schema.json`
- `schemas/style.schema.json`

**DELETE (Plugin skills):**
- `plugins/csharp-compounding-docs/skills/` (entire directory)

**MODIFY `McpServerBuilder.cs`:** Remove calls to `AddAgentServices()`, `AddSkillServices()`, or similar DI wiring for the old infrastructure.

---

## Phase 13: Rewrite Tests — 100% Code Coverage with Aspire Orchestration

**Goal:** 100% code coverage across all remaining source code. All container orchestration uses **.NET Aspire** — no Testcontainers, no localstack. Aspire's `DistributedApplicationTestingBuilder` manages Neo4j, OpenSearch, and WireMock containers as part of the test AppHost.

### Why Not localstack

localstack lacks openCypher support for Neptune (Gremlin only), doesn't support OpenSearch Serverless, and its Bedrock emulation proxies to Ollama with wrong models/response formats. It adds paid tier cost while providing poor fidelity for all three target services.

### Testing Stack

| Concern | Tool | Rationale |
|---------|------|-----------|
| Neptune (openCypher) | **Aspire `AddContainer`** → `neo4j:5-community` | Neptune uses Bolt + openCypher; Neo4j is the reference impl. Same `Neo4j.Driver` NuGet in prod. Aspire manages container lifecycle. |
| OpenSearch (k-NN) | **Aspire `AddContainer`** → `opensearchproject/opensearch:2.19.0` | Official image includes k-NN plugin. Aspire manages container + port mapping. |
| Bedrock (LLM + Embed) | **Aspire `AddContainer`** → `wiremock/wiremock:latest` | WireMock container with mounted stub mappings. AWS SDK `ServiceURL` overridden to point at WireMock endpoint. |
| Unit mocking | **NSubstitute** | Interface mocking for fast unit tests (no containers). |
| E2E Orchestration | **Aspire.Hosting.Testing** (`DistributedApplicationTestingBuilder`) | Single test AppHost orchestrates all containers + MCP server project. |
| Coverage | **coverlet** + CI gate | `dotnet test /p:CollectCoverage=true /p:Threshold=100` — fail build if <100%. |

### Test AppHost Project

Create a dedicated test AppHost that wires up all containers for integration/e2e tests:

**`tests/CompoundDocs.Tests.AppHost/Program.cs`:**
```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Neo4j as Neptune stand-in (openCypher via Bolt protocol)
var neo4j = builder.AddContainer("neo4j", "neo4j", "5-community")
    .WithEnvironment("NEO4J_AUTH", "neo4j/testpassword")
    .WithHttpEndpoint(targetPort: 7474, name: "http")
    .WithEndpoint(targetPort: 7687, name: "bolt", scheme: "tcp");

// OpenSearch with k-NN plugin (included by default in official image)
var opensearch = builder.AddContainer("opensearch", "opensearchproject/opensearch", "2.19.0")
    .WithEnvironment("discovery.type", "single-node")
    .WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
    .WithEnvironment("OPENSEARCH_INITIAL_ADMIN_PASSWORD", "Test_Pass1!")
    .WithHttpEndpoint(targetPort: 9200, name: "http");

// WireMock as Bedrock stub (mounted JSON response fixtures)
var wiremock = builder.AddContainer("bedrock-mock", "wiremock/wiremock", "latest")
    .WithBindMount("../TestFixtures/wiremock", "/home/wiremock")
    .WithHttpEndpoint(targetPort: 8080, name: "http");

// MCP Server project under test — references all backends
var mcpServer = builder.AddProject<Projects.CompoundDocs_McpServer>("mcp-server")
    .WithReference(neo4j.GetEndpoint("bolt"))
    .WithReference(opensearch.GetEndpoint("http"))
    .WithReference(wiremock.GetEndpoint("http"))
    .WithEnvironment("Bedrock__ServiceURL", wiremock.GetEndpoint("http"));

builder.Build().Run();
```

### WireMock Stub Fixtures

**`tests/TestFixtures/wiremock/mappings/bedrock-converse.json`** — Stubs `POST /model/anthropic.claude-*/converse` with recorded Bedrock Converse API response JSON.

**`tests/TestFixtures/wiremock/mappings/bedrock-embed.json`** — Stubs `POST /model/amazon.titan-embed-text-v2:0/invoke` with recorded Titan Embed V2 response (1024-dim float array).

These are real API responses captured once, stored as JSON fixtures, and replayed by WireMock. This validates SDK request serialization and response deserialization against actual API contracts.

### Test Project Structure

```
tests/
  CompoundDocs.Tests.AppHost/                 -- Aspire test AppHost (orchestrates all containers)
    Program.cs                                -- Wires Neo4j, OpenSearch, WireMock, MCP server
    CompoundDocs.Tests.AppHost.csproj

  TestFixtures/                               -- Shared test data
    wiremock/
      mappings/
        bedrock-converse.json                 -- Recorded Bedrock Converse response
        bedrock-embed.json                    -- Recorded Titan Embed V2 response
      __files/                                -- WireMock response body files (if needed)

  CompoundDocs.Tests.Unit/                    -- Fast, no containers, NSubstitute mocks
    Tools/
      RagQueryToolTests.cs                    -- All RagQueryTool logic paths
    Services/
      BedrockEmbeddingServiceTests.cs         -- Embedding service unit tests
      BedrockLlmServiceTests.cs               -- LLM service unit tests
      GraphRagPipelineTests.cs                -- Pipeline orchestration logic
    Models/
      CompoundDocumentTests.cs                -- POCO validation
      DocumentChunkTests.cs                   -- Chunk model tests
      ToolResponseTests.cs                    -- Response/error model tests
    Processing/
      MarkdownParserTests.cs                  -- Parser unit tests
      FrontmatterParserTests.cs               -- Frontmatter extraction
      DocumentProcessorTests.cs               -- Processing pipeline
    CompoundDocs.Tests.Unit.csproj

  CompoundDocs.Tests.Integration/             -- Aspire-orchestrated integration tests
    Graph/
      GraphRepositoryTests.cs                 -- openCypher CRUD against Neo4j via Aspire
      GraphTraversalTests.cs                  -- Multi-hop queries, entity relationships
    Vector/
      VectorStoreTests.cs                     -- k-NN index creation, vector search against OpenSearch
      EmbeddingIndexTests.cs                  -- Index mapping, 1024-dim validation
    Bedrock/
      BedrockLlmIntegrationTests.cs           -- SDK serialization against WireMock container
      BedrockEmbeddingIntegrationTests.cs     -- Titan Embed response format validation
    Pipeline/
      GraphRagPipelineIntegrationTests.cs     -- Full pipeline: embed → search → enrich → synthesize
    CompoundDocs.Tests.Integration.csproj

  CompoundDocs.Tests.E2E/                     -- Full system tests via Aspire
    McpProtocolComplianceTests.cs             -- MCP server registers 1 tool, handles MCP requests
    EndToEndWorkflowTests.cs                  -- rag_query flow through entire system
    CompoundDocs.Tests.E2E.csproj
```

### NuGet Packages

```xml
<!-- CompoundDocs.Tests.AppHost.csproj -->
<PackageReference Include="Aspire.Hosting" />
<PackageReference Include="Aspire.Hosting.Testing" />
<!-- ProjectReference to CompoundDocs.McpServer for Projects.CompoundDocs_McpServer -->

<!-- CompoundDocs.Tests.Unit.csproj -->
<PackageReference Include="NSubstitute" />
<PackageReference Include="xunit" />
<PackageReference Include="Microsoft.NET.Test.Sdk" />
<PackageReference Include="coverlet.collector" />
<PackageReference Include="FluentAssertions" />

<!-- CompoundDocs.Tests.Integration.csproj -->
<PackageReference Include="Aspire.Hosting.Testing" />
<PackageReference Include="NSubstitute" />
<PackageReference Include="xunit" />
<PackageReference Include="Microsoft.NET.Test.Sdk" />
<PackageReference Include="coverlet.collector" />
<PackageReference Include="FluentAssertions" />
<PackageReference Include="Neo4j.Driver" />
<PackageReference Include="OpenSearch.Client" />
<!-- ProjectReference to CompoundDocs.Tests.AppHost -->

<!-- CompoundDocs.Tests.E2E.csproj -->
<PackageReference Include="Aspire.Hosting.Testing" />
<PackageReference Include="xunit" />
<PackageReference Include="Microsoft.NET.Test.Sdk" />
<PackageReference Include="coverlet.collector" />
<PackageReference Include="FluentAssertions" />
<!-- ProjectReference to CompoundDocs.Tests.AppHost -->
```

### Aspire Test Pattern (Integration + E2E)

All integration and E2E tests use `DistributedApplicationTestingBuilder` to start the test AppHost:

```csharp
public class GraphRepositoryTests : IAsyncLifetime
{
    private DistributedApplication _app;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.CompoundDocs_Tests_AppHost>();
        _app = await builder.BuildAsync();
        await _app.StartAsync();
    }

    [Fact]
    public async Task CreateDocumentNode_StoresInGraph()
    {
        // Get Neo4j bolt endpoint from Aspire
        var neo4jEndpoint = _app.GetEndpoint("neo4j", "bolt");
        var driver = GraphDatabase.Driver($"bolt://{neo4jEndpoint.Host}:{neo4jEndpoint.Port}",
            AuthTokens.Basic("neo4j", "testpassword"));

        // Test actual openCypher queries against real Neo4j
        // ...
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();
}
```

### Neo4j as Neptune Stand-in

Neptune uses Bolt protocol + openCypher. The same `Neo4j.Driver` NuGet package is used for both Neptune and Neo4j connections. Compatibility notes:
- Stick to openCypher-compliant subset (no APOC procedures, no Neo4j-specific DDL)
- Neptune doesn't support `CREATE CONSTRAINT` or `CREATE INDEX` in the same way
- For testing, this provides high fidelity for query validation

### DELETE (Obsolete Test Files)

All test files for deleted tools, infrastructure, and old formats:
- `tests/CompoundDocs.Tests/Tools/` — all except `RagQueryToolTests.cs` (which gets rewritten)
- `tests/CompoundDocs.Tests/DocTypes/` — entire directory
- `tests/CompoundDocs.Tests/Session/` — entire directory
- `tests/CompoundDocs.Tests/Storage/` — entire directory
- `tests/CompoundDocs.Tests/Configuration/McpServerOptionsTests.cs`
- `tests/CompoundDocs.Tests/Graph/DocumentLinkGraphTests.cs`
- `tests/CompoundDocs.Tests/Utilities/TestExternalDocumentBuilder.cs`
- `tests/CompoundDocs.Tests/Features/` — entire directory
- `tests/CompoundDocs.Tests/Agents/` — entire directory
- `tests/CompoundDocs.Tests/Skills/` — entire directory

### Coverage Enforcement

Add to `Directory.Build.props` or CI pipeline:
```xml
<PropertyGroup>
  <CollectCoverage>true</CollectCoverage>
  <CoverletOutputFormat>cobertura</CoverletOutputFormat>
  <Threshold>100</Threshold>
  <ThresholdType>line</ThresholdType>
  <ThresholdStat>total</ThresholdStat>
</PropertyGroup>
```

Run: `dotnet test --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByFile="**/Program.cs"` (exclude only the entry point bootstrap)

---

## Phase 14: Update Documentation

**MODIFY:**
- `docs/ARCHITECTURE.md` — single tool, Neptune+OpenSearch+Bedrock, emergent doc types, no tenant concept
- `docs/API-REFERENCE.md` — only `rag_query` tool
- `docs/configuration.md` — remove PostgreSQL config, update AWS config
- `docs/installation.md` — remove PostgreSQL/pgvector setup
- `docs/first-time-setup.md` — same
- `CLAUDE.md` — remove PostgreSQL build prereqs

---

## Phase 15: Update NEW-PLAN.md

Update `NEW-PLAN.md` to reflect all corrections:
- Section 2: Remove all tools except `rag_query` from tool mapping table. Remove "New Tools" section.
- Section 2: Remove `IDocumentRepository` from "Keep & Modify" items. Remove tenant/session references.
- Section 4: Remove `IDocumentRepository` interface. Update `IGraphRepository` (no repo/branch management). Remove `ISessionContext`/`IRequestContext`.
- Section 7: Rewrite to show single tool only. Remove skills/agents that no longer exist.
- Section 8: Remove PostgreSQL config. Remove tenant-related config (Auth/ApiKeys if tied to tenant model).
- All phases: Remove references to doc type registry, tenant infrastructure, PostgreSQL, multiple tools.

---

## Verification

After all changes:

### Build & Dead Code
1. `dotnet build` — entire solution compiles with zero warnings
2. `grep -r "pgvector\|Npgsql\|TenantKey\|ISessionContext\|DocTypeRegistry" src/` returns no hits
3. `grep -r "Testcontainers" tests/` returns no hits (Aspire only)
4. No references to deleted tool names (`ActivateProject`, `IndexDocument`, `SemanticSearch`, etc.) remain in source code
5. No `AgentLoader`, `AgentRegistry`, `SkillLoader`, `SkillExecutor` code remains in `src/`

### Testing
6. `dotnet test` — all unit, integration, and E2E tests pass
7. `dotnet test /p:CollectCoverage=true /p:Threshold=100` — 100% line coverage enforced
8. Integration tests start Neo4j, OpenSearch, WireMock containers via Aspire `DistributedApplicationTestingBuilder`
9. E2E tests exercise full MCP HTTP transport → `rag_query` → backends pipeline via Aspire

### Runtime
10. MCP server starts and registers exactly **1 tool** (`rag_query`)

### File System
11. `docker/` directory no longer exists
12. No `.yaml` files remain in `skills/` or `agents/` (old format)
13. Skills exist as `.claude/skills/cdocs-query/SKILL.md` and `.claude/skills/cdocs-help/SKILL.md`
14. Agents exist as `.claude/agents/*.md` files with correct YAML frontmatter
