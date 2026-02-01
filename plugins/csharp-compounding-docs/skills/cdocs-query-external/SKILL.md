---
name: cdocs:query-external
description: Performs read-only RAG query against external project documentation
allowed-tools:
  - Read
preconditions:
  - Project activated via /cdocs:activate
  - external_docs configured in .csharp-compounding-docs/config.json
---

# Query External Documentation Skill

## Intake

This skill accepts a natural language question about external project documentation. The user provides:

- **query** (required): The question or query to answer from external documentation
- **limit** (optional): Maximum number of source documents to retrieve (1-20, default: 5)
- **project_name** (optional): Specific external project to query (if multiple configured)
- **include_chunks** (optional): Whether to include individual content chunks in the response (default: false)

**Note**: This is a **read-only** skill. External documentation cannot be created or modified through this plugin. The assumption is that external docs are maintained via an external process.

## Process

1. **Check Configuration**: Verify that `external_docs` is configured in `.csharp-compounding-docs/config.json`
   - If not configured, inform user and offer guidance on configuration
   - Configuration should specify external project paths and embedding settings
2. **Validate Input**: Ensure the query meets minimum length requirements (3+ characters)
3. **Call MCP Tool**: Invoke the `rag_query_external` MCP tool with the provided parameters
4. **Process Response**: The RAG system will:
   - Convert the query to embeddings
   - Retrieve semantically relevant chunks from external document vector store
   - Use LLM to synthesize a coherent answer from the retrieved chunks
   - Return the answer with external source attribution and confidence scoring
5. **Format Results**: Present the answer with external sources and metadata
6. **Offer Follow-up**: Provide option to read external source documents for deeper investigation

## Output Format

The skill returns a markdown-formatted response containing:

### Query Results Section
- **Query**: The original question asked
- **External Project**: Name of the external project queried
- **Answer**: Synthesized answer from the RAG system based on external docs

### Sources Section
- List of external source documents with:
  - Document title
  - External file path
  - Relevance score

### Content Chunks Section (optional)
- Individual text chunks retrieved from the external vector store
- Only included if `include_chunks=true`

### Metadata
- **Confidence Score**: RAG system's confidence in the answer (0.0-1.0)

## Examples

### Example 1: Basic External Query
```
User: "How does the platform's rate limiting work?"

Skill invocation:
- query: "How does the platform's rate limiting work?"
- limit: 5

Output:
# Query Results - External Documentation

**Query**: How does the platform's rate limiting work?
**External Project**: MyCompany.Platform

## Answer

The platform implements a token bucket rate limiting algorithm with different tiers
based on subscription level. Free tier allows 100 requests per hour, while enterprise
tier allows 10,000 requests per hour. Rate limits are applied per API key and reset
at the top of each hour. When limits are exceeded, the API returns HTTP 429 with a
Retry-After header indicating when requests can resume.

## Sources

- [Rate Limiting Architecture](external/platform-docs/architecture/rate-limiting.md) (relevance: 0.96)
- [API Quotas and Limits](external/platform-docs/guides/quotas.md) (relevance: 0.89)
- [API Reference - Rate Limit Headers](external/platform-docs/api/headers.md) (relevance: 0.82)

---
**Confidence**: 0.93
```

### Example 2: External Query - Not Configured
```
User: "What authentication methods does the external API support?"

Output:
# External Documentation Not Configured

**Query**: What authentication methods does the external API support?

External documentation query is not currently configured for this project.

To enable external documentation querying, add an `external_docs` section to your
`.csharp-compounding-docs/config.json`:

\`\`\`json
{
  "external_docs": {
    "enabled": true,
    "projects": [
      {
        "name": "MyCompany.Platform",
        "path": "/path/to/external/docs",
        "description": "Platform API documentation"
      }
    ]
  }
}
\`\`\`

Would you like help configuring external documentation access?
```

### Example 3: Detailed External Query with Chunks
```
User: "Explain the webhook delivery retry mechanism in detail"

Skill invocation:
- query: "webhook delivery retry mechanism"
- limit: 3
- include_chunks: true

Output:
# Query Results - External Documentation

**Query**: webhook delivery retry mechanism
**External Project**: MyCompany.Platform

## Answer

The webhook delivery system uses an exponential backoff retry strategy. When a webhook
delivery fails (non-2xx response or timeout), the system will retry up to 5 times with
increasing delays: 1 minute, 5 minutes, 15 minutes, 1 hour, and 6 hours. After all
retries are exhausted, the webhook is marked as failed and an alert is sent to the
configured notification channel. Webhooks are considered delivered only on receiving
a 2xx response within the 30-second timeout window.

## Sources

- [Webhook Architecture](external/platform-docs/architecture/webhooks.md) (relevance: 0.97)
- [Webhook Reliability Guide](external/platform-docs/guides/webhook-reliability.md) (relevance: 0.91)
- [Webhook Configuration](external/platform-docs/api/webhook-config.md) (relevance: 0.84)

## Content Chunks

### Chunk 0
Webhook delivery uses exponential backoff for retries. The retry schedule is:
Attempt 1: 1 minute after failure, Attempt 2: 5 minutes after failure,
Attempt 3: 15 minutes after failure, Attempt 4: 1 hour after failure,
Attempt 5: 6 hours after failure. After 5 failed attempts, the webhook is
marked as permanently failed.

### Chunk 1
A webhook delivery is considered successful only when the receiving endpoint
returns an HTTP status code in the 2xx range (200-299) within 30 seconds.
Timeouts, connection errors, and non-2xx responses all trigger the retry mechanism.

### Chunk 2
When all retry attempts are exhausted, the system sends a failure notification
to the configured alert channel. You can monitor webhook delivery status via
the dashboard or API. Failed webhooks can be manually retried from the console.

---
**Confidence**: 0.95
```

### Example 4: Multiple External Projects
```
User: "How do I authenticate with the payment gateway?"

Skill invocation:
- query: "payment gateway authentication"
- project_name: "PaymentService"
- limit: 5

Output:
# Query Results - External Documentation

**Query**: payment gateway authentication
**External Project**: PaymentService

## Answer

The payment gateway uses API key authentication with request signing. Each request
must include an X-API-Key header with your merchant API key, and an X-Signature
header containing an HMAC-SHA256 signature of the request body using your secret key.
The signature ensures request integrity and authenticity. Keys are rotated every 90
days for security.

## Sources

- [Authentication Overview](external/payment-docs/auth/overview.md) (relevance: 0.94)
- [Request Signing Guide](external/payment-docs/guides/signing.md) (relevance: 0.90)
- [Security Best Practices](external/payment-docs/security/best-practices.md) (relevance: 0.81)

---
**Confidence**: 0.91
```

### Example 5: Decision Matrix Usage

**Use RAG Query External (this skill) when:**
- Question is open-ended about external project: "How does their authentication work?"
- Multiple external docs likely relevant
- Synthesis needed across external sources
- User wants an answer from external docs, not just discovery
- Need comprehensive understanding of external system behavior

**Use Search External (/cdocs:search-external) when:**
- Query is specific to external project: "Find the external API rate limits doc"
- Looking for one specific external document
- User explicitly asks to "find" or "search" external docs
- Discovery of what external documentation exists
- Browsing rather than question answering

## Notes

- This is a manual-invocation skill (no auto-triggers)
- Requires active project context via `/cdocs:activate`
- Requires `external_docs` configuration in `.csharp-compounding-docs/config.json`
- Uses the `rag_query_external` MCP tool for retrieval-augmented generation
- **READ-ONLY**: Cannot create, modify, or delete external documentation
- External docs are assumed to be maintained by external processes/teams
- Sources are ranked by semantic relevance to the query
- Confidence scores indicate RAG system's certainty in the synthesized answer
- Useful for understanding external APIs, dependencies, and related projects
- Can query multiple external projects if configured
- Synthesizes information across multiple external documents
- External source paths may be absolute or relative to configured project root
