# Claude API Embeddings Research: Definitive Findings and RAG Implementation Guide

## Executive Summary

**Anthropic does NOT provide an embeddings API.** This is definitively confirmed in their official documentation. For RAG implementations using Claude as the generation model, you must use a separate embedding provider. Anthropic officially recommends [Voyage AI](https://www.voyageai.com/) as their preferred embedding partner.

---

## 1. Claude Embedding Capabilities

### Definitive Answer: No Native Embeddings API

According to the [official Claude documentation](https://platform.claude.com/docs/en/build-with-claude/embeddings):

> "Anthropic does not offer its own embedding model."

**Key Points:**
- Anthropic has no embeddings endpoint
- No announced plans to add embeddings (as of January 2026)
- This is a deliberate partnership strategy, not a gap

### Anthropic's Official Stance

Anthropic has partnered with Voyage AI as their recommended embedding provider. The documentation states:

> "One embeddings provider that has a wide variety of options and capabilities is Voyage AI. Voyage AI makes state-of-the-art embedding models and offers customized models for specific industry domains such as finance and healthcare, or bespoke fine-tuned models for individual customers."

---

## 2. Current Anthropic API Endpoints

Based on the [API Overview documentation](https://platform.claude.com/docs/en/api/overview), here are all available endpoints:

### Generally Available APIs

| API | Endpoint | Description |
|-----|----------|-------------|
| Messages API | `POST /v1/messages` | Send messages to Claude for conversational interactions |
| Message Batches API | `POST /v1/messages/batches` | Process large volumes asynchronously (50% cost reduction) |
| Token Counting API | `POST /v1/messages/count_tokens` | Count tokens before sending |
| Models API | `GET /v1/models` | List available Claude models |

### Beta APIs

| API | Endpoint | Description |
|-----|----------|-------------|
| Files API | `POST /v1/files` | Upload and manage files |
| Skills API | `POST /v1/skills` | Create and manage custom agent skills |

### What Does NOT Exist

- **Embeddings API** - Does not exist
- **Completions API** - Claude uses Messages API only (no legacy completions)
- **Fine-tuning API** - Not publicly available

---

## 3. Alternative Embedding Solutions for Claude RAG

### Recommended Options Comparison

| Provider | Model | Dimensions | Price/1M tokens | Best For |
|----------|-------|------------|-----------------|----------|
| **Voyage AI** (Anthropic recommended) | voyage-3.5 | 1024 | ~$0.06 | Claude integration |
| **Voyage AI** | voyage-3-large | 1024-2048 | ~$0.12 | Best quality |
| **Voyage AI** | voyage-code-3 | 1024 | ~$0.06 | Code RAG |
| **OpenAI** | text-embedding-3-large | 3072 | $0.13 | General purpose |
| **OpenAI** | text-embedding-3-small | 1536 | $0.02 | Cost-effective |
| **Cohere** | embed-v4 | 1024 | $0.10 | Multilingual |
| **Nomic** | nomic-embed-text | 768 | $0.05 / Free (local) | Budget/Local |
| **Sentence-Transformers** | all-MiniLM-L6-v2 | 384 | Free (local) | Fast prototyping |

### Voyage AI Models (Anthropic's Partner)

From the [Voyage AI documentation](https://docs.voyageai.com/docs/embeddings):

| Model | Context Length | Description |
|-------|---------------|-------------|
| `voyage-3.5` | 32,000 | Balanced performance, recommended for most use cases |
| `voyage-3-large` | 32,000 | Best general-purpose and multilingual retrieval |
| `voyage-3.5-lite` | 32,000 | Optimized for latency and cost |
| `voyage-code-3` | 32,000 | Optimized for code retrieval |
| `voyage-finance-2` | 32,000 | Finance domain |
| `voyage-law-2` | 16,000 | Legal domain |
| `voyage-multimodal-3` | 32,000 | Text and images |

### Local/Open-Source Options

**Ollama with nomic-embed-text:**
- 274MB model size
- 2048 context length
- 768 embedding dimensions
- Runs entirely locally
- Source: [Ollama library](https://ollama.com/library/nomic-embed-text)

---

## 4. Hybrid Architecture Patterns

### Architecture Diagram

```
                    +------------------+
                    |   User Query     |
                    +--------+---------+
                             |
                             v
                    +------------------+
                    | Embedding Model  |  <-- OpenAI / Voyage AI / Ollama
                    | (Query Embed)    |
                    +--------+---------+
                             |
                             v
                    +------------------+
                    |  Vector Database |  <-- Qdrant / Pinecone / pgvector
                    |  (Similarity     |
                    |   Search)        |
                    +--------+---------+
                             |
                             v
                    +------------------+
                    |  Retrieved Docs  |
                    +--------+---------+
                             |
                             v
                    +------------------+
                    |   Claude API     |  <-- Generation with context
                    |   (Messages)     |
                    +--------+---------+
                             |
                             v
                    +------------------+
                    |    Response      |
                    +------------------+
```

### Cost Considerations

| Operation | Provider | Cost Estimate |
|-----------|----------|---------------|
| Embed 1M tokens (docs) | Voyage AI voyage-3.5 | ~$0.06 |
| Embed 1M tokens (docs) | OpenAI text-embedding-3-small | $0.02 |
| Claude generation (1M input tokens) | Claude 3.5 Sonnet | $3.00 |
| Claude generation (1M output tokens) | Claude 3.5 Sonnet | $15.00 |

**Key insight:** Embedding costs are typically negligible compared to generation costs.

### Latency Implications

1. **Two API calls required:** One for embedding, one for generation
2. **Embedding latency:** ~50-200ms depending on provider
3. **Claude generation:** ~500-2000ms depending on response length
4. **Vector search:** ~10-50ms with optimized databases
5. **Total overhead:** ~100-300ms added to response time

---

## 5. Anthropic's RAG Recommendations

### Contextual Retrieval

Anthropic published their [Contextual Retrieval](https://www.anthropic.com/news/contextual-retrieval) approach, which significantly improves RAG performance:

**The Problem:** Traditional RAG chunks lose context. Example: "The company's revenue grew by 3%" doesn't specify which company.

**The Solution:** Prepend contextual summaries to each chunk before embedding:

```
Original chunk: "The company's revenue grew by 3% over the previous quarter."

Contextualized chunk: "This chunk is from Acme Corp's Q3 2024 earnings report.
The company's revenue grew by 3% over the previous quarter."
```

**Performance Improvements:**
| Technique | Failure Rate Reduction |
|-----------|----------------------|
| Contextual Embeddings alone | 35% (5.7% to 3.7%) |
| + Contextual BM25 | 49% (5.7% to 2.9%) |
| + Reranking | 67% (5.7% to 1.9%) |

### Citations API (Alternative to Traditional RAG)

The [Citations API](https://platform.claude.com/docs/en/build-with-claude/citations) is a powerful alternative or complement to RAG:

**How it works:**
1. Pass documents directly to Claude with `citations: {"enabled": true}`
2. Claude automatically extracts and cites relevant passages
3. Responses include exact source references with character/page locations

**Usage example:**
```python
response = client.messages.create(
    model="claude-sonnet-4-5",
    max_tokens=1024,
    messages=[{
        "role": "user",
        "content": [
            {
                "type": "document",
                "source": {
                    "type": "text",
                    "media_type": "text/plain",
                    "data": "Your document content here..."
                },
                "citations": {"enabled": True}
            },
            {
                "type": "text",
                "text": "What does this document say about X?"
            }
        ]
    }]
)
```

**Advantages over prompt-based citations:**
- Cost savings: `cited_text` doesn't count toward output tokens
- Better reliability: Citations are guaranteed valid
- Improved quality: More likely to cite relevant quotes

**Supported document types:**
- Plain text (chunked into sentences, 0-indexed character citations)
- PDF (chunked into sentences, 1-indexed page citations)
- Custom content (your own chunks, 0-indexed block citations)

**Limitation:** Cannot be used with Structured Outputs

---

## 6. Embedding Model Comparison for Claude RAG

### MTEB Benchmark Rankings (2025)

Based on [MTEB Leaderboard](https://huggingface.co/spaces/mteb/leaderboard) data:

| Rank | Model | MTEB Score | Dimensions |
|------|-------|------------|------------|
| 1 | Cohere embed-v4 | 65.2 | 1024 |
| 2 | OpenAI text-embedding-3-large | 64.6 | 3072 |
| 3 | Voyage AI voyage-3-large | 63.8 | 1024-2048 |
| 4 | BGE-M3 | 63.0 | 1024 |
| 5 | E5-Mistral-7B | 61.8 | 4096 |
| 6 | nomic-embed-text-v1.5 | 59.4 | 768 |
| 7 | all-MiniLM-L6-v2 | 56.3 | 384 |
| 8 | OpenAI text-embedding-3-small | 55.8 | 1536 |

### Dimension Size Tradeoffs

| Dimensions | Storage | Search Speed | Quality | Recommended For |
|------------|---------|--------------|---------|-----------------|
| 256-384 | Minimal | Fastest | Good | Prototyping, edge devices |
| 768-1024 | Moderate | Fast | Very Good | Production, balanced |
| 1536-2048 | Higher | Moderate | Excellent | High-stakes applications |
| 3072+ | Highest | Slower | Best | Maximum accuracy |

### Best Pairings with Claude

**For general RAG:**
- Voyage AI `voyage-3.5` (Anthropic's recommendation)
- OpenAI `text-embedding-3-small` (cost-effective)

**For code RAG:**
- Voyage AI `voyage-code-3`

**For legal/finance domains:**
- Voyage AI `voyage-law-2` or `voyage-finance-2`

**For local/private deployments:**
- Ollama with `nomic-embed-text`
- Sentence-Transformers `all-MiniLM-L6-v2`

---

## 7. C# Implementation

### Required NuGet Packages

```xml
<!-- Core packages -->
<PackageReference Include="Anthropic.SDK" Version="5.9.0" />

<!-- Choose ONE embedding option: -->

<!-- Option A: OpenAI Embeddings -->
<PackageReference Include="Azure.AI.OpenAI" Version="2.1.0" />
<!-- OR -->
<PackageReference Include="OpenAI" Version="2.1.0" />

<!-- Option B: Voyage AI (via HTTP) -->
<PackageReference Include="System.Net.Http.Json" Version="9.0.0" />

<!-- Option C: Ollama (local) -->
<PackageReference Include="OllamaSharp" Version="5.4.8" />

<!-- Vector database (choose one) -->
<PackageReference Include="Qdrant.Client" Version="1.11.0" />
<!-- OR -->
<PackageReference Include="Pinecone.NET" Version="2.0.0" />

<!-- Optional: Semantic Kernel for orchestration -->
<PackageReference Include="Microsoft.SemanticKernel" Version="1.30.0" />
```

### Complete Hybrid RAG Implementation

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace ClaudeRagExample;

// ============================================
// EMBEDDING PROVIDERS
// ============================================

public interface IEmbeddingProvider
{
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default);
    Task<float[][]> GetEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default);
}

// Option 1: Voyage AI Embeddings (Anthropic's recommended partner)
public class VoyageAiEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public VoyageAiEmbeddingProvider(string apiKey, string model = "voyage-3.5")
    {
        _model = model;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.voyageai.com/v1/")
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var result = await GetEmbeddingsAsync(new[] { text }, ct);
        return result[0];
    }

    public async Task<float[][]> GetEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var request = new
        {
            input = texts.ToArray(),
            model = _model,
            input_type = "document"
        };

        var response = await _httpClient.PostAsJsonAsync("embeddings", request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var embeddings = json.GetProperty("data")
            .EnumerateArray()
            .OrderBy(x => x.GetProperty("index").GetInt32())
            .Select(x => x.GetProperty("embedding")
                .EnumerateArray()
                .Select(v => v.GetSingle())
                .ToArray())
            .ToArray();

        return embeddings;
    }

    public void Dispose() => _httpClient.Dispose();
}

// Option 2: OpenAI Embeddings
public class OpenAiEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OpenAiEmbeddingProvider(string apiKey, string model = "text-embedding-3-small")
    {
        _model = model;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var result = await GetEmbeddingsAsync(new[] { text }, ct);
        return result[0];
    }

    public async Task<float[][]> GetEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var request = new
        {
            input = texts.ToArray(),
            model = _model
        };

        var response = await _httpClient.PostAsJsonAsync("embeddings", request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var embeddings = json.GetProperty("data")
            .EnumerateArray()
            .OrderBy(x => x.GetProperty("index").GetInt32())
            .Select(x => x.GetProperty("embedding")
                .EnumerateArray()
                .Select(v => v.GetSingle())
                .ToArray())
            .ToArray();

        return embeddings;
    }

    public void Dispose() => _httpClient.Dispose();
}

// Option 3: Ollama Local Embeddings
public class OllamaEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OllamaEmbeddingProvider(string model = "nomic-embed-text", string baseUrl = "http://localhost:11434")
    {
        _model = model;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var request = new { model = _model, prompt = text };
        var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return json.GetProperty("embedding")
            .EnumerateArray()
            .Select(v => v.GetSingle())
            .ToArray();
    }

    public async Task<float[][]> GetEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var tasks = texts.Select(t => GetEmbeddingAsync(t, ct));
        return await Task.WhenAll(tasks);
    }

    public void Dispose() => _httpClient.Dispose();
}

// ============================================
// SIMPLE IN-MEMORY VECTOR STORE
// ============================================

public record Document(string Id, string Content, float[] Embedding, Dictionary<string, string>? Metadata = null);

public class SimpleVectorStore
{
    private readonly List<Document> _documents = new();

    public void AddDocument(Document doc) => _documents.Add(doc);

    public void AddDocuments(IEnumerable<Document> docs) => _documents.AddRange(docs);

    public IEnumerable<(Document Doc, double Score)> Search(float[] queryEmbedding, int topK = 5)
    {
        return _documents
            .Select(doc => (Doc: doc, Score: CosineSimilarity(queryEmbedding, doc.Embedding)))
            .OrderByDescending(x => x.Score)
            .Take(topK);
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}

// ============================================
// RAG SERVICE
// ============================================

public class ClaudeRagService : IDisposable
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly SimpleVectorStore _vectorStore;
    private readonly AnthropicClient _claudeClient;
    private readonly string _model;

    public ClaudeRagService(
        IEmbeddingProvider embeddingProvider,
        string anthropicApiKey,
        string claudeModel = "claude-sonnet-4-5")
    {
        _embeddingProvider = embeddingProvider;
        _vectorStore = new SimpleVectorStore();
        _claudeClient = new AnthropicClient(anthropicApiKey);
        _model = claudeModel;
    }

    // Index documents with contextual retrieval (Anthropic's recommended approach)
    public async Task IndexDocumentsAsync(
        IEnumerable<(string id, string content, string? context)> documents,
        CancellationToken ct = default)
    {
        var docList = documents.ToList();

        // Apply contextual retrieval: prepend context to each chunk
        var textsToEmbed = docList
            .Select(d => string.IsNullOrEmpty(d.context)
                ? d.content
                : $"{d.context}\n\n{d.content}")
            .ToArray();

        var embeddings = await _embeddingProvider.GetEmbeddingsAsync(textsToEmbed, ct);

        for (int i = 0; i < docList.Count; i++)
        {
            var doc = new Document(
                docList[i].id,
                docList[i].content,
                embeddings[i],
                new Dictionary<string, string> { ["context"] = docList[i].context ?? "" }
            );
            _vectorStore.AddDocument(doc);
        }
    }

    // Query with RAG
    public async Task<string> QueryAsync(
        string question,
        int topK = 5,
        CancellationToken ct = default)
    {
        // 1. Embed the query
        var queryEmbedding = await _embeddingProvider.GetEmbeddingAsync(question, ct);

        // 2. Retrieve relevant documents
        var results = _vectorStore.Search(queryEmbedding, topK).ToList();

        // 3. Build context from retrieved documents
        var context = string.Join("\n\n---\n\n",
            results.Select((r, i) => $"[Document {i + 1}] (relevance: {r.Score:F3})\n{r.Doc.Content}"));

        // 4. Generate response with Claude
        var systemPrompt = @"You are a helpful assistant that answers questions based on the provided context.
Always cite which document(s) you're drawing information from.
If the context doesn't contain relevant information, say so clearly.";

        var userMessage = $@"Context:
{context}

Question: {question}

Please answer based on the context provided above.";

        var response = await _claudeClient.Messages.CreateAsync(new MessageParameters
        {
            Model = _model,
            MaxTokens = 2048,
            System = new List<SystemMessage> { new(systemPrompt) },
            Messages = new List<Message>
            {
                new(RoleType.User, userMessage)
            }
        }, ct);

        return response.Content.FirstOrDefault()?.Text ?? "No response generated.";
    }

    public void Dispose()
    {
        if (_embeddingProvider is IDisposable disposable)
            disposable.Dispose();
    }
}

// ============================================
// USAGE EXAMPLE
// ============================================

public class Program
{
    public static async Task Main()
    {
        // Choose your embedding provider:

        // Option 1: Voyage AI (Anthropic's recommended partner)
        // var embeddingProvider = new VoyageAiEmbeddingProvider(
        //     Environment.GetEnvironmentVariable("VOYAGE_API_KEY")!);

        // Option 2: OpenAI
        var embeddingProvider = new OpenAiEmbeddingProvider(
            Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
            "text-embedding-3-small");

        // Option 3: Ollama (local, requires: ollama pull nomic-embed-text)
        // var embeddingProvider = new OllamaEmbeddingProvider("nomic-embed-text");

        var ragService = new ClaudeRagService(
            embeddingProvider,
            Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!);

        // Index documents with contextual retrieval
        var documents = new[]
        {
            ("doc1", "The Mediterranean diet emphasizes fish, olive oil, and vegetables.",
                "This is from a health and nutrition guide about diet recommendations."),
            ("doc2", "Photosynthesis converts light energy into glucose in plants.",
                "This is from a biology textbook chapter on plant processes."),
            ("doc3", "Claude is an AI assistant made by Anthropic.",
                "This is from Anthropic's official documentation about their AI products."),
            ("doc4", "Vector embeddings represent text as numerical vectors for similarity search.",
                "This is from a machine learning course on natural language processing.")
        };

        await ragService.IndexDocumentsAsync(documents);

        // Query
        var response = await ragService.QueryAsync("What is Claude and who made it?");
        Console.WriteLine("Response:");
        Console.WriteLine(response);
    }
}
```

---

## Summary and Recommendations

### Key Takeaways

1. **Claude has no embeddings API** - This is confirmed and intentional
2. **Use Voyage AI** if you want Anthropic's recommended solution
3. **Use OpenAI text-embedding-3-small** for cost-effective general purpose
4. **Use Ollama/nomic-embed-text** for fully local deployments
5. **Consider Citations API** for smaller document sets that fit in context
6. **Implement Contextual Retrieval** for significant RAG quality improvements

### Recommended Architecture for C# Projects

```
Production RAG Stack:
- Embeddings: Voyage AI voyage-3.5 or OpenAI text-embedding-3-small
- Vector DB: Qdrant, Pinecone, or Azure AI Search
- Generation: Claude (claude-sonnet-4-5 or claude-3-5-sonnet)
- Framework: Anthropic.SDK + custom embedding provider
```

```
Local/Development Stack:
- Embeddings: Ollama with nomic-embed-text
- Vector DB: Qdrant (local Docker) or in-memory
- Generation: Claude API
- Framework: OllamaSharp + Anthropic.SDK
```

---

## Sources

- [Claude Embeddings Documentation](https://platform.claude.com/docs/en/build-with-claude/embeddings)
- [Claude API Overview](https://platform.claude.com/docs/en/api/overview)
- [Anthropic Contextual Retrieval](https://www.anthropic.com/news/contextual-retrieval)
- [Claude Citations API](https://platform.claude.com/docs/en/build-with-claude/citations)
- [Voyage AI Documentation](https://docs.voyageai.com/docs/embeddings)
- [Anthropic.SDK GitHub](https://github.com/tghamm/Anthropic.SDK)
- [OllamaSharp GitHub](https://github.com/awaescher/OllamaSharp)
- [Microsoft Azure OpenAI Embeddings](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/embeddings)
- [Semantic Kernel Embedding Services](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/embedding-generation/)
- [MTEB Leaderboard](https://huggingface.co/spaces/mteb/leaderboard)
- [Ollama nomic-embed-text](https://ollama.com/library/nomic-embed-text)

---

*Research compiled: January 2026*
