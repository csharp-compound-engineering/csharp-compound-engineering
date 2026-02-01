# First-Time Setup Guide

Welcome to CSharp Compound Docs! This guide walks you through the initial setup after installation.

## Prerequisites

Before starting, ensure you have:
- Completed the [Installation Guide](./installation.md)
- MCP server running and connected
- Database initialized with pgvector

---

## Step 1: Activate Your Project

The first step is to activate a project context. This tells the plugin where to find and store your documentation.

```
/cdocs-activate
```

You'll be prompted to:
1. Enter your project name
2. Specify the documentation root directory
3. Configure document types to track

### Project Configuration

The activation creates a `.compound-docs/` directory in your project with:

```
.compound-docs/
├── config.json          # Project configuration
├── schemas/             # Custom document type schemas
└── docs/                # Indexed documentation output
    ├── problems/        # Problem documents
    ├── insights/        # Insight documents
    ├── codebase/        # Codebase documents
    ├── tools/           # Tool documents
    └── styles/          # Style documents
```

---

## Step 2: Configure Document Types

CSharp Compound Docs comes with five built-in document types:

| Type | Purpose | Example Use |
|------|---------|-------------|
| **problem** | Solved bugs, issues, errors | "Why was the auth failing?" |
| **insight** | Product/project learnings | "Users prefer dark mode" |
| **codebase** | Architecture decisions | "We use CQRS because..." |
| **tool** | Library gotchas, configs | "EF Core migrations require..." |
| **style** | Coding conventions | "Always use async/await" |

### Custom Document Types

You can create custom types using:

```
/cdocs-create-type
```

This will prompt you for:
- Type name (e.g., "api-endpoint")
- Required fields
- Optional fields
- Auto-invoke patterns

---

## Step 3: Capture Your First Document

Let's capture a simple problem document. Use one of these methods:

### Method A: Direct Skill Invocation

```
/cdocs-problem
```

The skill will guide you through:
1. Describing the problem symptoms
2. Explaining the root cause
3. Documenting the solution
4. Adding relevant tags

### Method B: Auto-Invoke

Simply mention a problem in conversation:

> "I fixed the null reference exception in the UserService - the issue was that we weren't checking for null before accessing the Claims property."

The plugin will detect keywords like "fixed", "issue was", "null reference" and offer to capture it.

### Method C: Interactive Selection

```
/cdocs-capture-select
```

This shows all available document types and lets you choose which one to capture.

---

## Step 4: Query Your Knowledge Base

After capturing documents, you can query them:

### Semantic Search

```
/cdocs-search authentication issues
```

Returns documents semantically related to your query.

### RAG Query

```
/cdocs-query How do we handle user authentication?
```

Uses RAG to synthesize an answer from your documentation.

### External Search

```
/cdocs-search-external Entity Framework migrations
```

Searches external documentation sources.

---

## Step 5: Organize with Promotion Levels

Documents have three promotion levels that affect their ranking in search results:

| Level | Priority | Use Case |
|-------|----------|----------|
| **pinned** | Highest | Critical knowledge, always relevant |
| **promoted** | Medium | Important, frequently accessed |
| **standard** | Normal | Regular documentation |

Promote important documents:

```
/cdocs-promote
```

---

## Recommended Workflow

### During Development

1. **Capture as you code** - When you solve a problem or learn something, capture it immediately
2. **Use auto-invoke** - Let the plugin detect capture opportunities from your conversation
3. **Query before searching** - Check your knowledge base before external searches

### Regular Maintenance

1. **Review and promote** - Periodically promote frequently-useful documents
2. **Archive old content** - Use `/cdocs-delete` for outdated information
3. **Add tags** - Improve discoverability with consistent tagging

### Team Collaboration

1. **Share the database** - Point team members to the same PostgreSQL instance
2. **Standardize types** - Agree on document type usage conventions
3. **Cross-reference** - Link related documents for better context

---

## Keyboard Shortcuts

When using Claude Code, these shortcuts help with common operations:

| Action | Shortcut |
|--------|----------|
| Quick capture | Type `/cdocs-` then Tab for completion |
| Search | `/cdocs-search <query>` |
| Query | `/cdocs-query <question>` |

---

## Configuration Tips

### Embedding Quality

For better search results:

1. **Use appropriate chunk sizes** - Default 1000 chars works for most content
2. **Set overlap** - 200 char overlap prevents context loss at boundaries
3. **Choose the right model** - `nomic-embed-text` for local, `text-embedding-3-small` for OpenAI

### Performance Tuning

For large knowledge bases:

1. **Index incrementally** - Don't reindex everything at once
2. **Use batching** - Default batch size of 10 balances speed and memory
3. **Monitor queue** - Check deferred indexing if Ollama is slow

---

## Common Patterns

### Capturing During Code Review

When reviewing code, capture insights:

> "This service uses the Repository pattern with Unit of Work - it's important because it allows us to transaction multiple operations."

Use `/cdocs-codebase` to capture architecture decisions.

### Documenting Tool Issues

When you hit a library quirk:

> "Entity Framework requires explicit loading for filtered includes - you can't use .Include(x => x.Items.Where(...))"

Use `/cdocs-tool` to capture the gotcha.

### Recording Style Decisions

When the team agrees on a convention:

> "We always use CancellationToken as the last parameter in async methods"

Use `/cdocs-style` to capture the convention.

---

## Next Steps

- Explore all skills with `/cdocs-help`
- Set up [git worktrees](./git-worktree-integration.md) for multi-branch work
- Configure [external documentation sources](./external-sources.md)
- Review the [API Reference](./API-REFERENCE.md) for MCP tools
