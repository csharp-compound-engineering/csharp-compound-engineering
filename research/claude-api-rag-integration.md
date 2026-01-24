# Claude API Integration for RAG Applications

## Research Summary

This document provides comprehensive guidance for integrating Claude API into a C# RAG (Retrieval-Augmented Generation) application, with focus on Claude Opus models.

---

## 1. Authentication Methods

### API Keys

**How to Obtain:**
1. Create an account at the [Anthropic Console](https://console.anthropic.com)
2. Navigate to "API Keys" section in the dashboard (usually in left sidebar or Settings menu)
3. Click "Create Key" or "Generate New Key"
4. Name your key descriptively (e.g., "Production Backend", "Testing Environment")
5. Set permissions, assign to workspace, and configure model access

**Critical Security Notes:**
- API keys are shown **only once** - store immediately in a secure location
- Never commit API keys to source control
- Store in password managers, secure vaults, or environment variables
- Rotate keys immediately if compromised

**Environment Variable Convention:**
```bash
# Standard environment variable names
ANTHROPIC_API_KEY=sk-ant-api03-xxxxx...
ANTHROPIC_AUTH_TOKEN=alternative_auth_token  # Alternative
ANTHROPIC_BASE_URL=https://api.anthropic.com  # Default endpoint
```

### Authentication Header Format
```http
X-Api-Key: YOUR_API_KEY
anthropic-version: 2023-06-01
Content-Type: application/json
```

### Subscription vs API Access

**Important Distinction:**
- **Claude Pro/Max subscriptions** ($20-$200/month) provide access to Claude on web, desktop, and mobile apps
- **API access** requires **separate** billing through the Claude Console
- A Claude Pro subscription does NOT include API usage
- API usage is billed separately on a pay-as-you-go basis

**Claude Code Exception:** If you have a Pro or Max plan, Claude Code CLI can use your subscription. However, if `ANTHROPIC_API_KEY` is set, it will use API credits instead.

Sources:
- [Anthropic Claude API Key Guide](https://www.nightfall.ai/ai-security-101/anthropic-claude-api-key)
- [Claude Help Center - Subscription vs API](https://support.claude.com/en/articles/9876003-i-have-a-paid-claude-subscription-pro-max-team-or-enterprise-plans-why-do-i-have-to-pay-separately-to-use-the-claude-api-and-console)

---

## 2. API Access Models

### Direct Anthropic API
- **Endpoint:** `https://api.anthropic.com`
- **Global by default** - no regional routing options
- **Full feature support** including all beta features
- **Standard pricing** (no regional premium)

### AWS Bedrock
- **Integration:** Deep AWS ecosystem integration
- **Endpoint Types:**
  - Global endpoints: Dynamic routing for maximum availability
  - Regional endpoints: Data routing within specific geographic regions (+10% premium)
- **Additional Features:**
  - Intelligent Prompt Routing (can reduce costs by up to 30%)
  - Provisioned Throughput Mode for production workloads
  - Enterprise-grade SLAs and compliance controls
- **Pricing:** Same base pricing as Anthropic API; Provisioned Throughput minimum ~$15,000/month

### Google Cloud Vertex AI
- **Integration:** Deep GCP ecosystem integration
- **Endpoint Types:** Global + Regional (+10% premium for regional)
- **Additional Features:**
  - AutoML capabilities
  - Custom model training pipelines
  - Advanced MLOps workflows

### Comparison Table

| Aspect | Anthropic API | AWS Bedrock | Google Vertex AI |
|--------|---------------|-------------|------------------|
| Endpoint | Global only | Global + Regional (+10%) | Global + Regional (+10%) |
| Base Pricing | Standard | Same as Anthropic | Same as Anthropic |
| Multi-model | Claude only | Multiple vendors | Google + third-party |
| Enterprise Features | Enterprise tier | SLAs, compliance | MLOps, AutoML |

Sources:
- [Claude Docs - Pricing](https://platform.claude.com/docs/en/about-claude/pricing)
- [AWS Bedrock Pricing](https://aws.amazon.com/bedrock/pricing/)

---

## 3. Claude Opus Availability and Specifications

### Model IDs

| Model | Model ID | Status |
|-------|----------|--------|
| Claude Opus 4 | `claude-opus-4-20250522` | Legacy |
| Claude Opus 4.1 | `claude-opus-4-1-YYYYMMDD` | Available |
| Claude Opus 4.5 | `claude-opus-4-5-20251101` | **Recommended** |

**Note:** Opus 4.x rate limits are shared across all Opus 4, 4.1, and 4.5 variants.

### Pricing (Per Million Tokens)

| Model | Input | Output |
|-------|-------|--------|
| Claude Opus 4 (Legacy) | $15.00 | $75.00 |
| **Claude Opus 4.5** | **$5.00** | **$25.00** |
| Claude Sonnet 4.5 (standard) | $3.00 | $15.00 |
| Claude Sonnet 4.5 (>200K input) | $6.00 | $22.50 |
| Claude Haiku 4.5 | $0.80 | $4.00 |

**Batch API:** 50% discount on both input and output tokens for asynchronous processing.

### Context Windows

| Model | Standard Context | Extended Context |
|-------|-----------------|------------------|
| All Claude models | 200,000 tokens | Up to 1M tokens (Sonnet 4.x only, Tier 4+) |

**Notes:**
- 1M token context window is in beta for Tier 4 organizations
- Extended context has premium pricing for tokens >200K
- Extended thinking mode uses entire context window (200K tokens)

### Rate Limits by Tier

**Tier 1 (Starting tier, $5 credit purchase):**

| Model | RPM | ITPM | OTPM |
|-------|-----|------|------|
| Claude Opus 4.x | 50 | 30,000 | 8,000 |
| Claude Sonnet 4.x | 50 | 30,000 | 8,000 |
| Claude Haiku 4.5 | 50 | 50,000 | 10,000 |

**Tier 2 ($40 cumulative credit purchase):**

| Model | RPM | ITPM | OTPM |
|-------|-----|------|------|
| Claude Opus 4.x | 1,000 | 450,000 | 90,000 |
| Claude Sonnet 4.x | 1,000 | 450,000 | 90,000 |
| Claude Haiku 4.5 | 1,000 | 450,000 | 90,000 |

**Tier 3 ($200 cumulative credit purchase):**

| Model | RPM | ITPM | OTPM |
|-------|-----|------|------|
| Claude Opus 4.x | 2,000 | 800,000 | 160,000 |
| Claude Sonnet 4.x | 2,000 | 800,000 | 160,000 |
| Claude Haiku 4.5 | 2,000 | 1,000,000 | 200,000 |

**Tier 4 ($400 cumulative credit purchase):**

| Model | RPM | ITPM | OTPM |
|-------|-----|------|------|
| Claude Opus 4.x | 4,000 | 2,000,000 | 400,000 |
| Claude Sonnet 4.x | 4,000 | 2,000,000 | 400,000 |
| Claude Haiku 4.5 | 4,000 | 4,000,000 | 800,000 |

Sources:
- [Claude Docs - Rate Limits](https://platform.claude.com/docs/en/api/rate-limits)
- [Claude Docs - Pricing](https://platform.claude.com/docs/en/about-claude/pricing)

---

## 4. RAG Implementation Considerations

### Anthropic's Citations API

Anthropic provides a built-in **Citations API** specifically designed for RAG applications:

**Key Features:**
- Automatic source document linking
- Built-in citation extraction
- Reduced hallucinations through grounding
- No additional cost for `cited_text` in output tokens

**How It Works:**
1. Upload documents (PDF, plaintext) to Claude's context window
2. Enable Citations in your API request
3. Claude automatically chunks documents into sentences
4. Responses include specific passage citations

**Benefits:**
- Up to 15% increase in recall accuracy vs custom implementations
- Cost savings (cited text doesn't count as output tokens)
- Reduced source hallucinations (customers report 10% to 0% reduction)

### Contextual RAG Best Practices

**Anthropic's Recommended Approach:**

1. **Chunk Augmentation:** Prepend explanatory context to each chunk using a small, cost-effective LLM
2. **Hybrid Search:** Combine sparse (keyword/BM25) and dense (semantic) embeddings
3. **Rank Fusion:** Use Reciprocal Rank Fusion (RRF) to combine results
4. **Two-Stage Retrieval:**
   - Retrieve top 150 chunks
   - Rerank to top 20 chunks
   - Pass to Claude for answer generation

### Structuring RAG Prompts

**System Prompt Pattern:**
```
You are an assistant that answers questions based on the provided context.
Only use information from the context to answer. If the answer is not in
the context, say "I don't have enough information to answer that."
```

**Context Injection:**
```
<context>
[Retrieved document chunks here]
</context>

User question: {user_question}
```

**With Citations Enabled:**
```json
{
  "model": "claude-opus-4-5-20251101",
  "messages": [...],
  "documents": [
    {
      "type": "document",
      "source": {
        "type": "text",
        "content": "Your document content here"
      }
    }
  ]
}
```

### Context Engineering Principles

From Anthropic's guidance: **"Claude is already smart enough - intelligence is not the bottleneck, context is."**

Key principles:
- Curate context carefully - a large context window isn't useful if poorly managed
- Use prompt caching for repeated content (system prompts, tool definitions)
- Structure context hierarchically
- Remove irrelevant information to improve signal-to-noise ratio

Sources:
- [Anthropic Citations Documentation](https://platform.claude.com/docs/en/build-with-claude/citations)
- [Simon Willison's Citations API Analysis](https://simonwillison.net/2025/Jan/24/anthropics-new-citations-api/)
- [Contextual RAG Implementation Guide](https://docs.together.ai/docs/how-to-implement-contextual-rag-from-anthropic)

---

## 5. Streaming vs Non-Streaming Responses

### Non-Streaming (Default)
```json
{
  "model": "claude-opus-4-5-20251101",
  "max_tokens": 1024,
  "messages": [{"role": "user", "content": "Hello, Claude"}]
}
```

**Use Cases:**
- Backend processing
- Batch operations
- When complete response is needed before proceeding

### Streaming (Server-Sent Events)
```json
{
  "model": "claude-opus-4-5-20251101",
  "max_tokens": 1024,
  "messages": [{"role": "user", "content": "Hello, Claude"}],
  "stream": true
}
```

**Event Types:**
- `message_start` - Beginning of message
- `content_block_start` - Beginning of content block
- `content_block_delta` - Incremental content
- `content_block_stop` - End of content block
- `message_delta` - Message-level updates
- `message_stop` - End of message
- `ping` - Keep-alive events

**Use Cases:**
- Real-time user interfaces
- Long-running generations
- Better perceived responsiveness

Sources:
- [Claude Docs - Streaming](https://platform.claude.com/docs/en/build-with-claude/streaming)

---

## 6. SDK Options

### Official Anthropic C# SDK (Recommended)

**Package:** `Anthropic` on NuGet

```bash
dotnet add package Anthropic
```

**Status:** Currently in Beta (as of early 2026)

**Basic Usage:**
```csharp
using Anthropic;
using Anthropic.Models.Messages;

// Using environment variable (ANTHROPIC_API_KEY)
AnthropicClient client = new();

// Or explicit configuration
AnthropicClient client = new() { ApiKey = "your-api-key" };

// Create a message
MessageCreateParams parameters = new()
{
    MaxTokens = 1024,
    Messages =
    [
        new()
        {
            Role = Role.User,
            Content = "Hello, Claude",
        },
    ],
    Model = Model.ClaudeOpus4_5_20251101, // or use string: "claude-opus-4-5-20251101"
};

var message = await client.Messages.Create(parameters);
Console.WriteLine(message);
```

**Streaming:**
```csharp
await foreach (var evt in client.Messages.CreateStreaming(parameters))
{
    Console.WriteLine(evt);
}
```

**Microsoft.Extensions.AI Integration:**
```csharp
using Microsoft.Extensions.AI;

IChatClient chatClient = client.AsIChatClient("claude-opus-4-5-20251101")
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();
```

**Error Handling:**
```csharp
try
{
    var message = await client.Messages.Create(parameters);
}
catch (AnthropicRateLimitException ex)
{
    // Handle 429 rate limit errors
    var retryAfter = ex.RetryAfter;
}
catch (AnthropicUnauthorizedException ex)
{
    // Handle 401 authentication errors
}
catch (AnthropicApiException ex)
{
    // Handle other API errors
}
```

**Automatic Retries:** SDK retries 2 times by default on connection errors, 408, 409, 429, and 5xx errors.

### Unofficial Community SDKs

**Anthropic.SDK** (by tghamm):
- NuGet: `Anthropic.SDK`
- Targets: .NET Standard 2.0, .NET 8.0, .NET 10.0
- Features: Streaming, Tools, Batching, Semantic Kernel integration
- GitHub: https://github.com/tghamm/Anthropic.SDK

**Claudia** (by Cysharp):
- NuGet: `Claudia`
- Targets: .NET Standard 2.1, .NET 6.0, .NET 8.0
- Features: Source generator for function calling
- GitHub: https://github.com/Cysharp/Claudia

### Direct REST API

For simple integrations without SDK dependencies:

```csharp
using System.Net.Http.Json;

var client = new HttpClient();
client.DefaultRequestHeaders.Add("x-api-key", Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

var request = new
{
    model = "claude-opus-4-5-20251101",
    max_tokens = 1024,
    messages = new[]
    {
        new { role = "user", content = "Hello, Claude" }
    }
};

var response = await client.PostAsJsonAsync(
    "https://api.anthropic.com/v1/messages",
    request
);

var result = await response.Content.ReadFromJsonAsync<JsonDocument>();
```

Sources:
- [Anthropic C# SDK GitHub](https://github.com/anthropics/anthropic-sdk-csharp)
- [Official SDK Documentation](https://platform.claude.com/docs/en/api/client-sdks)

---

## 7. Pricing and Billing

### Billing Model
- **Pay-as-you-go:** No fixed subscription required for API
- **Token-based:** Charged per input and output tokens
- **Separate from subscriptions:** API billing is completely separate from Claude Pro/Max subscriptions

### Tier System

| Tier | Credit Purchase Required | Max Single Purchase | Monthly Spend Limit |
|------|-------------------------|---------------------|---------------------|
| Tier 1 | $5 | $100 | $100 |
| Tier 2 | $40 | $500 | $500 |
| Tier 3 | $200 | $1,000 | $1,000 |
| Tier 4 | $400 | $5,000 | $5,000 |
| Monthly Invoicing | Contact sales | N/A | Custom |

### Cost Optimization Strategies

1. **Prompt Caching:** Cached tokens cost 10% of base input price and don't count toward ITPM limits
2. **Batch API:** 50% discount for asynchronous processing
3. **Model Selection:** Use Haiku for simple tasks, Sonnet for balanced workloads, Opus for complex reasoning
4. **Context Efficiency:** Minimize unnecessary context to reduce input token costs

### Example Cost Calculation (Claude Opus 4.5)

| Usage Pattern | Input Tokens | Output Tokens | Cost |
|--------------|--------------|---------------|------|
| Simple query | 1,000 | 500 | $0.0175 |
| RAG with context | 50,000 | 2,000 | $0.30 |
| Large document | 200,000 | 4,000 | $1.10 |

Sources:
- [Claude Pricing](https://claude.com/pricing)
- [Claude Docs - Pricing](https://platform.claude.com/docs/en/about-claude/pricing)

---

## 8. Enterprise and Team Options

### Enterprise Features

**Authentication & Identity:**
- SAML 2.0 and OIDC-based SSO
- Forced login methods via configuration
- Organization-scoped repository tokens

**Access Control:**
- Self-serve seat management
- Granular spend controls (organization and user level)
- Workspace-level API key management
- IP allowlists for API calls

**Compliance & Audit:**
- Compliance API for programmatic access to usage data
- Zero Data Retention (ZDR) for Enterprise API traffic
- Real-time monitoring capabilities
- AES-256 encryption at rest, TLS 1.2+ in transit
- BYOK (Bring Your Own Key) coming H1 2026

**Claude Code for Enterprise:**
- Included in Team and Enterprise plans
- OAuth authentication flow
- Configurable to force enterprise credentials
- CI/CD integration support

### Team Plans

- Centralized billing and management
- Shared API quotas across team members
- Admin panel for user provisioning
- Usage analytics and reporting

Sources:
- [Anthropic Enterprise](https://www.anthropic.com/enterprise)
- [Claude for Enterprise Announcement](https://www.anthropic.com/news/claude-for-enterprise)

---

## 9. Complete C# RAG Example

```csharp
using Anthropic;
using Anthropic.Models.Messages;

public class ClaudeRagService
{
    private readonly AnthropicClient _client;
    private readonly IVectorStore _vectorStore;

    public ClaudeRagService(string apiKey, IVectorStore vectorStore)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _vectorStore = vectorStore;
    }

    public async Task<string> QueryWithContext(string userQuestion, int topK = 5)
    {
        // 1. Retrieve relevant documents from vector store
        var relevantDocs = await _vectorStore.SearchAsync(userQuestion, topK);

        // 2. Build context from retrieved documents
        var context = string.Join("\n\n---\n\n",
            relevantDocs.Select((doc, i) => $"[Document {i+1}]: {doc.Content}"));

        // 3. Construct the prompt with RAG context
        var systemPrompt = @"You are a helpful assistant that answers questions based on the provided context.
Always cite which document(s) you used to answer the question.
If the answer is not in the context, say ""I don't have enough information to answer that question.""";

        var userMessage = $@"<context>
{context}
</context>

Question: {userQuestion}";

        // 4. Call Claude API
        var parameters = new MessageCreateParams
        {
            MaxTokens = 2048,
            System = systemPrompt,
            Messages = new[]
            {
                new Message
                {
                    Role = Role.User,
                    Content = userMessage,
                }
            },
            Model = Model.ClaudeOpus4_5_20251101,
        };

        var response = await _client.Messages.Create(parameters);

        return response.Content[0].Text;
    }

    public async IAsyncEnumerable<string> QueryWithContextStreaming(
        string userQuestion,
        int topK = 5)
    {
        var relevantDocs = await _vectorStore.SearchAsync(userQuestion, topK);
        var context = string.Join("\n\n---\n\n",
            relevantDocs.Select((doc, i) => $"[Document {i+1}]: {doc.Content}"));

        var parameters = new MessageCreateParams
        {
            MaxTokens = 2048,
            System = "You are a helpful assistant...",
            Messages = new[]
            {
                new Message
                {
                    Role = Role.User,
                    Content = $"<context>\n{context}\n</context>\n\nQuestion: {userQuestion}",
                }
            },
            Model = Model.ClaudeOpus4_5_20251101,
        };

        await foreach (var evt in _client.Messages.CreateStreaming(parameters))
        {
            if (evt is ContentBlockDelta delta && delta.Delta?.Text != null)
            {
                yield return delta.Delta.Text;
            }
        }
    }
}
```

---

## 10. Quick Reference

### Model IDs
```
claude-opus-4-5-20251101     # Recommended Opus
claude-sonnet-4-5-20250929   # Balanced option
claude-haiku-4-5-20250929    # Fast/cheap option
```

### Environment Variables
```bash
ANTHROPIC_API_KEY=sk-ant-api03-...
ANTHROPIC_BASE_URL=https://api.anthropic.com  # Optional
```

### API Endpoint
```
POST https://api.anthropic.com/v1/messages
```

### Required Headers
```
x-api-key: YOUR_API_KEY
anthropic-version: 2023-06-01
content-type: application/json
```

### Official Documentation Links
- [API Reference](https://platform.claude.com/docs/en/api)
- [Building with Claude](https://platform.claude.com/docs/en/build-with-claude)
- [Citations](https://platform.claude.com/docs/en/build-with-claude/citations)
- [Streaming](https://platform.claude.com/docs/en/build-with-claude/streaming)
- [Rate Limits](https://platform.claude.com/docs/en/api/rate-limits)
- [Pricing](https://platform.claude.com/docs/en/about-claude/pricing)
- [C# SDK GitHub](https://github.com/anthropics/anthropic-sdk-csharp)

---

*Research compiled: January 2026*
