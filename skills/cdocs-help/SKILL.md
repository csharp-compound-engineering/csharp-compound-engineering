---
name: cdocs-help
description: Show help for the CompoundDocs knowledge base system. Use when the user asks about available cdocs commands or how to use the knowledge base.
disable-model-invocation: true
---

# CompoundDocs Help

## Available Skills

| Skill | Description |
|-------|-------------|
| `/cd:cdocs-query [question]` | Query the knowledge base for project documentation and patterns |

## Usage

Ask any question about the project and `/compound-docs:cdocs-query` will search the knowledge base:

```
/cd:cdocs-query How is authentication configured?
```

The knowledge base is automatically populated by the background worker from monitored git repositories.
