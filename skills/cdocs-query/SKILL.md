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
