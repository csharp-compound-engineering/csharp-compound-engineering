# Project Overview

## Purpose
CSharp Compound Engineering - A C# implementation of a documentation compounding system with an MCP (Model Context Protocol) server for managing compound documents with RAG (Retrieval-Augmented Generation) capabilities.

## Tech Stack
- **.NET 9** - Target framework
- **C# 12** - Language version with file-scoped namespaces
- **Model Context Protocol (MCP)** - For AI tool integration via `ModelContextProtocol.Server`
- **Semantic Kernel** - For embeddings and vector store operations
- **PostgreSQL + pgvector** - Vector database for document storage
- **Entity Framework Core** - ORM for data access
- **YamlDotNet** - YAML frontmatter parsing
- **Markdig** - Markdown parsing
- **Serilog** - Structured logging

## Project Structure
- `src/CompoundDocs.McpServer/` - Main MCP server implementation
  - `Tools/` - MCP tool implementations
  - `Services/` - Document processing, file watching services
  - `Session/` - Session and tenant management
  - `Models/` - Domain models (CompoundDocument, DocumentTypes)
  - `Data/` - Repository pattern for data access
  - `SemanticKernel/` - Embedding service integration
- `src/CompoundDocs.Common/` - Shared utilities
  - `Parsing/` - Frontmatter and markdown parsing
  - `Configuration/` - Configuration management
  - `Graph/` - Document link graph
- `src/CompoundDocs.Cleanup/` - Cleanup utility

## Document Types
The system supports 9 built-in document types:
- spec, adr, research, doc, problem, insight, codebase, tool, style

## Promotion Levels
- standard (1.0x boost)
- promoted (1.5x boost)
- pinned (2.0x boost)
