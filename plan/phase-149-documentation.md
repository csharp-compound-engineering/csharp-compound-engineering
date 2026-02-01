# Phase 149: Documentation Review

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Final Integration
> **Prerequisites**: All previous phases (1-148)

---

## Spec References

This phase covers comprehensive documentation review as defined in:

- **SPEC.md** - Complete specification overview and all linked documents
- **spec/marketplace.md** - [Plugin Manifest](../spec/marketplace.md#plugin-manifest) (lines 46-115)
- **spec/marketplace.md** - [Installation Flow](../spec/marketplace.md#installation-flow) (lines 180-234)
- **spec/marketplace.md** - [External MCP Server Prerequisites](../spec/marketplace.md#external-mcp-server-prerequisites) (lines 238-407)
- **spec/skills.md** - [Built-in Skills](../spec/skills.md#built-in-skills) documentation reference
- **spec/configuration.md** - Configuration options and schema documentation

---

## Objectives

1. Review and verify README.md completeness for end-user onboarding
2. Audit installation guide accuracy against actual installation process
3. Validate configuration documentation matches implemented schema
4. Ensure skill usage documentation covers all 17 skills with examples
5. Create comprehensive troubleshooting guide for common issues
6. Verify all documentation cross-references are valid and up-to-date
7. Audit user-facing error messages for clarity and actionability

---

## Acceptance Criteria

### README.md Completeness

- [ ] README.md exists at `plugins/csharp-compounding-docs/README.md`
- [ ] Executive summary clearly describes plugin purpose and value proposition
- [ ] Quick start guide allows new users to be productive within 5 minutes
- [ ] All 17 skills are listed with one-line descriptions
- [ ] Prerequisites section documents:
  - [ ] .NET 10 SDK requirement
  - [ ] Docker requirement
  - [ ] PowerShell 7 requirement
  - [ ] Required external MCP servers (Context7, Microsoft Learn, Sequential Thinking)
- [ ] Feature highlights section covers key capabilities
- [ ] Table of contents with anchor links for long sections
- [ ] Badge section includes CI status, version, and license badges
- [ ] Links to detailed documentation for each topic area

### Installation Guide Review

- [ ] Installation instructions match actual `claude plugin install` workflow
- [ ] All three installation scopes documented (user, project, workspace)
- [ ] MCP server auto-registration via `.mcp.json` explained
- [ ] Post-installation verification steps provided
- [ ] Update instructions (`claude plugin update`) documented
- [ ] Uninstallation instructions provided
- [ ] Docker infrastructure setup documented:
  - [ ] First-run initialization with `start-infrastructure.ps1`
  - [ ] Container health check verification
  - [ ] PostgreSQL connection verification
  - [ ] Ollama model availability verification
- [ ] Manual MCP server configuration documented for troubleshooting

### Configuration Documentation

- [ ] Global configuration (`~/.claude/.csharp-compounding-docs/config.json`) documented
- [ ] Project configuration (`./csharp-compounding-docs/config.json`) documented
- [ ] All configuration fields documented with:
  - [ ] Field name and type
  - [ ] Default value
  - [ ] Valid value ranges/options
  - [ ] Example usage
- [ ] Configuration precedence order documented (project > global > defaults)
- [ ] Environment variable overrides documented (`CDOCS_*` prefix)
- [ ] Schema validation errors and how to resolve them documented
- [ ] Sample configuration files provided for common scenarios

### Skill Usage Documentation

- [ ] Each of 17 skills has dedicated documentation section
- [ ] **Capture Skills** (5 skills):
  - [ ] `/cdocs:problem` - Usage, auto-invoke triggers, example output
  - [ ] `/cdocs:insight` - Usage, auto-invoke triggers, example output
  - [ ] `/cdocs:codebase` - Usage, auto-invoke triggers, example output
  - [ ] `/cdocs:tool` - Usage, auto-invoke triggers, example output
  - [ ] `/cdocs:style` - Usage, auto-invoke triggers, example output
- [ ] **Query Skills** (4 skills):
  - [ ] `/cdocs:query` - Usage, decision matrix explanation, example queries
  - [ ] `/cdocs:search` - Usage, semantic search examples
  - [ ] `/cdocs:search-external` - Usage, external project access
  - [ ] `/cdocs:query-external` - Usage, cross-project RAG
- [ ] **Meta Skills** (3 skills):
  - [ ] `/cdocs:activate` - Auto-invoke on project entry explained
  - [ ] `/cdocs:create-type` - Custom doc-type creation workflow
  - [ ] `/cdocs:capture-select` - Multi-trigger resolution explained
- [ ] **Utility Skills** (5 skills):
  - [ ] `/cdocs:delete` - Bulk deletion by project/branch/path
  - [ ] `/cdocs:promote` - Promotion levels (local, standard, shared) explained
  - [ ] `/cdocs:todo` - File-based todo tracking workflow
  - [ ] `/cdocs:worktree` - Git worktree integration
  - [ ] `/cdocs:research` - Research agent orchestration
- [ ] Each skill includes:
  - [ ] Purpose statement
  - [ ] Invocation syntax (explicit and auto-invoke if applicable)
  - [ ] Parameter descriptions
  - [ ] At least one example conversation
  - [ ] Expected output/behavior

### Troubleshooting Guide

- [ ] Troubleshooting guide exists at `plugins/csharp-compounding-docs/docs/TROUBLESHOOTING.md`
- [ ] **Installation Issues** section covers:
  - [ ] Plugin installation failures
  - [ ] .NET SDK not found
  - [ ] Docker not running
  - [ ] PowerShell version issues
- [ ] **MCP Server Issues** section covers:
  - [ ] Server fails to start
  - [ ] Port conflicts
  - [ ] Connection refused errors
  - [ ] Timeout errors
- [ ] **Docker Infrastructure Issues** section covers:
  - [ ] Container startup failures
  - [ ] PostgreSQL connection errors
  - [ ] pgvector extension issues
  - [ ] Ollama model download failures
  - [ ] Disk space issues
- [ ] **External MCP Dependencies** section covers:
  - [ ] Context7 configuration issues
  - [ ] Microsoft Learn MCP issues
  - [ ] Sequential Thinking MCP issues
  - [ ] SessionStart hook warning resolution
- [ ] **Indexing Issues** section covers:
  - [ ] File watcher not detecting changes
  - [ ] Documents not appearing in search
  - [ ] Embedding generation failures
  - [ ] Startup reconciliation issues
- [ ] **Query Issues** section covers:
  - [ ] RAG returning no results
  - [ ] Low relevance scores
  - [ ] Slow query performance
  - [ ] Ollama model errors
- [ ] Each issue includes:
  - [ ] Symptoms/error messages
  - [ ] Probable causes
  - [ ] Step-by-step resolution
  - [ ] Verification steps

### Documentation Cross-References

- [ ] All internal documentation links verified and functional
- [ ] All spec reference links verified
- [ ] All research document links verified
- [ ] Dead links identified and fixed or removed
- [ ] Bidirectional links where appropriate (e.g., README <-> TROUBLESHOOTING)

### Error Message Audit

- [ ] All user-facing error messages reviewed for clarity
- [ ] Error messages include actionable next steps
- [ ] Error codes documented in troubleshooting guide
- [ ] Stack traces are not exposed in user-facing errors
- [ ] Error messages reference relevant documentation sections

---

## Implementation Notes

### README.md Structure

The plugin README should follow this structure:

```markdown
# CSharp Compound Docs

> Capture and retrieve institutional knowledge with RAG-powered semantic search for C#/.NET projects

[![CI Status](badge-url)](ci-url)
[![Version](badge-url)](releases-url)
[![License: MIT](badge-url)](LICENSE)

## Table of Contents

- [Features](#features)
- [Quick Start](#quick-start)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Skills Reference](#skills-reference)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

## Features

- **Knowledge Capture**: Auto-detect and capture problems, insights, codebase knowledge, tools, and coding styles
- **RAG-Powered Retrieval**: Semantic search with context-aware answers
- **Multi-Tenant Support**: Isolated knowledge bases per project and branch
- **Git Worktree Integration**: Full support for concurrent development
- **Extensible Doc-Types**: Create custom documentation types with dedicated skills

## Quick Start

1. Install the plugin:
   ```bash
   claude plugin install csharp-compounding-docs@csharp-compound-marketplace
   ```

2. Navigate to your C# project and start a Claude Code session

3. The plugin auto-activates on project entry

4. Capture knowledge naturally during development:
   - Fix a bug? The plugin detects and offers to capture it
   - Learn something new about the codebase? Capture it as an insight
   - Find a useful library? Document it for future reference

5. Query your knowledge base:
   ```
   /cdocs:query How do we handle database connection pooling?
   ```

## Prerequisites

### Required

| Dependency | Version | Purpose |
|------------|---------|---------|
| .NET | 10.0+ | MCP server runtime |
| Docker | Latest | PostgreSQL/pgvector containers |
| PowerShell | 7.0+ | Launch scripts, automation |

### Required MCP Servers

Configure these in your `~/.claude/settings.json`:

**Context7** - Framework documentation lookup
...

**Microsoft Learn MCP** - .NET/C# documentation
...

**Sequential Thinking MCP** - Complex reasoning
...
```

### Skill Documentation Template

Each skill should be documented with this template:

```markdown
### /cdocs:{skill-name}

**Purpose**: One sentence describing what this skill does.

**Invocation**:
- Explicit: `/cdocs:{skill-name} [parameters]`
- Auto-invoke: Triggered by patterns like "fixed a bug", "the issue was"

**Parameters**:
| Parameter | Required | Description |
|-----------|----------|-------------|
| param1 | Yes | Description |
| param2 | No | Description (default: value) |

**Example**:

```
User: I just fixed the connection pooling issue. The problem was that...

[Plugin auto-detects and offers to capture]

User: /cdocs:problem

[Plugin creates documentation]
```

**Output**: Description of what gets created/returned.

**Related Skills**: Links to related skills.
```

### Troubleshooting Entry Template

Each troubleshooting entry should follow this template:

```markdown
### Issue: {Issue Title}

**Symptoms**:
- Symptom 1
- Symptom 2
- Error message: `Exact error message text`

**Probable Causes**:
1. Cause 1
2. Cause 2

**Resolution**:

1. Step 1
   ```bash
   command to run
   ```

2. Step 2
   ...

**Verification**:
```bash
# Verify the issue is resolved
verification command
```

**Related Issues**: Links to related troubleshooting entries.
```

### Documentation File Locations

After this phase, documentation exists at:

```
plugins/csharp-compounding-docs/
├── README.md                    # Main plugin documentation
├── docs/
│   ├── INSTALLATION.md          # Detailed installation guide
│   ├── CONFIGURATION.md         # Configuration reference
│   ├── SKILLS.md                # Skill usage documentation
│   ├── TROUBLESHOOTING.md       # Troubleshooting guide
│   └── CONTRIBUTING.md          # Contribution guidelines
└── CHANGELOG.md                 # Release history
```

### Documentation Quality Checklist

For each documentation file, verify:

1. **Accuracy**: Content matches actual implementation
2. **Completeness**: All features/options documented
3. **Clarity**: Written for target audience (developers new to the plugin)
4. **Examples**: Concrete examples for abstract concepts
5. **Formatting**: Consistent markdown styling
6. **Navigation**: Easy to find relevant sections
7. **Currency**: No outdated information

### External MCP Prerequisites Documentation

Per spec/marketplace.md, the README must prominently document required external MCP servers:

```markdown
## Prerequisites

Before using this plugin, ensure the following MCP servers are configured in your
`~/.claude/settings.json` or project `.claude/settings.json`:

### Required

**Context7** - Framework documentation lookup
```json
{
  "mcpServers": {
    "context7": {
      "type": "http",
      "url": "https://mcp.context7.com/mcp"
    }
  }
}
```

**Microsoft Learn MCP** - .NET/C# documentation lookup (remote HTTP endpoint)
```json
{
  "mcpServers": {
    "microsoft-learn": {
      "type": "sse",
      "url": "https://learn.microsoft.com/api/mcp"
    }
  }
}
```
> **Note**: Microsoft Learn MCP Server is a remote service - no npm package required.

**Sequential Thinking MCP** - Complex multi-step reasoning
```json
{
  "mcpServers": {
    "sequential-thinking": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  }
}
```
```

---

## Dependencies

### Depends On
- **All previous phases (1-148)**: Complete implementation must exist for documentation to be accurate

### Blocks
- **Phase 150**: Final testing and validation (documentation is prerequisite)
- **Release**: Documentation must pass review before release

---

## Verification Steps

After completing this phase, verify:

1. **README.md exists and is well-formed**:
   ```bash
   test -f plugins/csharp-compounding-docs/README.md && \
     markdown-lint plugins/csharp-compounding-docs/README.md
   ```

2. **All documentation files exist**:
   ```bash
   for f in INSTALLATION.md CONFIGURATION.md SKILLS.md TROUBLESHOOTING.md; do
     test -f "plugins/csharp-compounding-docs/docs/$f" && echo "$f: OK"
   done
   ```

3. **No broken internal links**:
   ```bash
   # Use markdown-link-check or similar tool
   find plugins/csharp-compounding-docs -name "*.md" -exec markdown-link-check {} \;
   ```

4. **All 17 skills documented**:
   ```bash
   # Verify each skill has a documentation section
   grep -c "### /cdocs:" plugins/csharp-compounding-docs/docs/SKILLS.md
   # Expected: 17
   ```

5. **Prerequisites documented**:
   ```bash
   grep -q "Context7" plugins/csharp-compounding-docs/README.md && \
   grep -q "Microsoft Learn" plugins/csharp-compounding-docs/README.md && \
   grep -q "Sequential Thinking" plugins/csharp-compounding-docs/README.md && \
   echo "Prerequisites: OK"
   ```

6. **Troubleshooting guide covers key areas**:
   ```bash
   for topic in "Installation" "MCP Server" "Docker" "Indexing" "Query"; do
     grep -q "$topic" plugins/csharp-compounding-docs/docs/TROUBLESHOOTING.md && \
       echo "$topic section: OK"
   done
   ```

7. **Configuration options documented**:
   ```bash
   # Compare documented config fields against schema
   jq -r '.properties | keys[]' spec/schemas/config.schema.json | while read field; do
     grep -q "$field" plugins/csharp-compounding-docs/docs/CONFIGURATION.md && \
       echo "$field: documented" || echo "$field: MISSING"
   done
   ```

8. **Spell check passes**:
   ```bash
   # Use aspell or similar
   find plugins/csharp-compounding-docs -name "*.md" -exec aspell check {} \;
   ```

---

## Notes

- Documentation should be written for developers who are new to the plugin but familiar with C#/.NET development
- Use consistent terminology throughout (e.g., always "document" not "doc", always "skill" not "command")
- Keep examples realistic and relevant to C#/.NET development scenarios
- Update CHANGELOG.md as documentation changes are made
- Consider adding screenshots or diagrams for complex workflows
- All example code should be tested and functional
- Documentation updates should be included in the same PR as related code changes going forward
- The troubleshooting guide will evolve based on user feedback post-release
