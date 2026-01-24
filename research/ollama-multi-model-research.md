# Ollama Multi-Model Research for RAG Workloads

**Research Date**: January 2026
**Purpose**: Running multiple models simultaneously in Ollama for RAG workloads requiring both embedding and LLM models

---

## Table of Contents

1. [Ollama Multi-Model Support](#1-ollama-multi-model-support)
2. [Concurrent Model Usage](#2-concurrent-model-usage)
3. [GPU Memory Management](#3-gpu-memory-management)
4. [Embedding + LLM Simultaneous Operation](#4-embedding--llm-simultaneous-operation)
5. [OLLAMA_NUM_PARALLEL Setting](#5-ollama_num_parallel-setting)
6. [Alternative: Two Ollama Instances](#6-alternative-two-ollama-instances)
7. [Docker Configuration for Multi-Instance](#7-docker-configuration-for-multi-instance)
8. [Performance Considerations](#8-performance-considerations)
9. [Recommended Architecture](#9-recommended-architecture)
10. [Complete Examples](#10-complete-examples)

---

## 1. Ollama Multi-Model Support

### Can Ollama Load Multiple Models Simultaneously?

**Yes.** Ollama supports loading and running multiple models at the same time, provided there is sufficient available memory (system RAM for CPU inference or VRAM for GPU inference).

### Key Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `OLLAMA_MAX_LOADED_MODELS` | 3 x GPU count (or 3 for CPU) | Maximum number of models that can be loaded concurrently |
| `OLLAMA_NUM_PARALLEL` | 4 or 1 (auto-selected based on memory) | Maximum parallel requests per model |
| `OLLAMA_MAX_QUEUE` | 512 | Maximum queued requests before returning 503 |
| `OLLAMA_KEEP_ALIVE` | 5m | Duration models stay loaded after last use |
| `OLLAMA_GPU_OVERHEAD` | 0 | Reserved VRAM per GPU (bytes) |
| `OLLAMA_SCHED_SPREAD` | false | Spread layers across all GPUs |
| `OLLAMA_LOAD_TIMEOUT` | 5m | Timeout for model loading |

### How Model Loading/Unloading Works

1. **Automatic Loading**: Models are loaded on first request
2. **Memory Check**: Before loading, Ollama checks if sufficient VRAM/RAM is available
3. **Idle Unloading**: Models are unloaded after `OLLAMA_KEEP_ALIVE` duration (default 5 minutes)
4. **Queue Management**: If memory is insufficient for a new model, requests are queued while idle models are unloaded
5. **FIFO Processing**: Queued requests are processed in first-in, first-out order

### Memory Requirements for Multiple Models

When using GPU inference, new models must be able to **completely fit in VRAM** to allow concurrent model loads. The model will not partially load to GPU if it cannot fit entirely.

**Sources:**
- [Ollama FAQ](https://docs.ollama.com/faq)
- [Ollama GitHub Issue #2109](https://github.com/ollama/ollama/issues/2109)

---

## 2. Concurrent Model Usage

### Making Requests to Different Models

Ollama supports two levels of concurrent processing:

1. **Multiple Models Loaded**: Different models can be loaded simultaneously
2. **Parallel Requests per Model**: Each loaded model can handle multiple concurrent requests

### How Ollama Handles Concurrent Requests to Different Models

- Each model operates independently once loaded
- Requests to different models are processed in parallel (if both are loaded)
- If a model needs to be loaded and memory is full, the request is queued
- Idle models are unloaded to make room for new model requests

### Model Switching Overhead and Latency

**Critical Performance Factor**: Model switching (unloading one model and loading another) introduces significant latency:

- **Cold Start Latency**: 10-30+ seconds depending on model size
- **VRAM Recovery**: Ollama waits for VRAM to be fully recovered before loading the next model
- **Recovery Timeout**: System polls for ~5 seconds for VRAM to recover after unload

### Keep-Alive Settings

The `OLLAMA_KEEP_ALIVE` environment variable controls how long models stay loaded:

| Value | Behavior |
|-------|----------|
| `5m` (default) | Unload after 5 minutes of inactivity |
| `1h` | Keep loaded for 1 hour |
| `-1` | **Keep loaded forever** (never unload automatically) |
| `0` | Unload immediately after response |

**API Override**: The `keep_alive` parameter in API requests overrides the environment variable:

```bash
# Keep model loaded forever via API
curl http://localhost:11434/api/generate -d '{"model": "llama3.2", "keep_alive": -1}'

# Command line
ollama run llama3 --keepalive -1

# Environment variable
OLLAMA_KEEP_ALIVE=-1 ollama serve
```

**Verification**: Use `ollama ps` to check loaded models. Look for `UNTIL: Forever` to confirm a model is pinned.

**Sources:**
- [Ollama FAQ](https://docs.ollama.com/faq)
- [How Ollama Handles Parallel Requests](https://www.glukhov.org/post/2025/05/how-ollama-handles-parallel-requests/)
- [GitHub Issue #5272 - keep_alive](https://github.com/ollama/ollama/issues/5272)

---

## 3. GPU Memory Management

### How Ollama Allocates GPU Memory

Ollama uses a sophisticated memory management system:

1. **Content-Addressable Storage**: Models are cached on disk with deduplication
2. **Dynamic GPU Allocation**: Memory is allocated per model based on requirements
3. **Iterative Refinement**: Uses multiple load operations to determine optimal memory layout:
   - `LoadOperationFit`: Estimates requirements without allocating
   - `LoadOperationAlloc`: Tests actual allocation
   - `LoadOperationCommit`: Final load with confirmed layout

### Multi-GPU Layer Assignment

For systems with multiple GPUs:

- **Greedy Packing**: By default, layers are packed onto as few GPUs as possible
- **Capacity Factor**: Binary search (0.0-1.0) balances distribution
- **Reverse Order Processing**: Output layers assigned first (typically larger)
- **OLLAMA_SCHED_SPREAD**: Set to `true` to spread layers across all GPUs

### Memory Components

Total model memory includes:
- **Model Weights**: Core parameters
- **KV Cache**: `context_size x kv_heads x head_dim x dtype_size x 2 (K+V) x parallel`
- **Compute Graphs**: Intermediate computation buffers
- **Token Embeddings**: Input/output embedding tables
- **Backend Minimums**: Runtime requirements

### What Happens When GPU Memory is Exhausted

1. New requests are **queued** (not rejected immediately)
2. Idle models are **unloaded** to free memory
3. VRAM recovery is **polled** with 5-second timeout
4. If queue exceeds `OLLAMA_MAX_QUEUE` (512), requests get **503 error**

### Automatic Model Unloading Behavior

- **Predicted VRAM**: Scheduler uses predicted usage (sum of all runners) rather than reported free memory
- **Lag Compensation**: GPU driver reporting can lag after model unload
- **Backoff Strategy**: Progressive backoff (0.0 to 1.0 in 0.1 increments) if allocation fails

### Known Issues

- **VRAM Recovery Timeout**: Users report "gpu VRAM usage didn't recover within timeout" errors
- **Memory Leak (pre-v0.7.0)**: Sporadic memory leak that fills VRAM even after unload
- **Runner Termination**: Ollama runner processes not always properly terminated

**Sources:**
- [DeepWiki - Memory Management and GPU Allocation](https://deepwiki.com/ollama/ollama/5.4-memory-management-and-gpu-allocation)
- [GitHub Issue #7130 - VRAM Timeout](https://github.com/ollama/ollama/issues/7130)
- [Ollama's Hidden VRAM Bug](https://medium.com/@rafal.kedziorski/ollamas-hidden-vram-bug-scripted-detection-and-cleanup-b3d6439d2199)

---

## 4. Embedding + LLM Simultaneous Operation

### Practical Scenarios for RAG

A typical RAG workflow requires:
1. **Embedding Model**: Generate vector embeddings for documents and queries
2. **LLM Model**: Generate responses using retrieved context

### Common Model Combinations

| Embedding Model | Size | VRAM Usage | LLM Model | Size | VRAM Usage | Total VRAM |
|-----------------|------|------------|-----------|------|------------|------------|
| nomic-embed-text | 137M params | ~0.5 GB | llama3.2:3b | 3.2B params | ~4.0 GB | ~4.5 GB |
| mxbai-embed-large | 334M params | ~1.2 GB | llama3.2:3b | 3.2B params | ~4.0 GB | ~5.2 GB |
| mxbai-embed-large | 334M params | ~1.2 GB | llama3.1:8b | 8B params | ~6-7 GB | ~7.5-8.2 GB |
| nomic-embed-text | 137M params | ~0.5 GB | mistral:7b | 7B params | ~6-7 GB | ~6.5-7.5 GB |

### Embedding Model Comparison

| Model | Parameters | Dimensions | Context Length | MTEB Score | Memory | Tokens/sec (RTX 4090) |
|-------|-----------|------------|----------------|------------|--------|----------------------|
| nomic-embed-text | 137M | 1,024 | 8,192 | 53.01 | 0.5 GB | 12,450 |
| mxbai-embed-large | 334M | 1,024 | 512 | 64.68 | 1.2 GB | 8,920 |

### Best Practices for RAG

1. **Keep Both Models Loaded**: Set `OLLAMA_KEEP_ALIVE=-1` for both models
2. **Pre-Load on Startup**: Send empty requests with `keep_alive: -1` to both models
3. **Use Smaller Embedding Model**: nomic-embed-text offers good balance of quality and efficiency
4. **Monitor VRAM**: Use `nvidia-smi` or `ollama ps` to verify both models fit

### Example Pre-Loading Script

```bash
# Pre-load embedding model
curl http://localhost:11434/api/embeddings -d '{
  "model": "nomic-embed-text",
  "prompt": "warmup",
  "keep_alive": -1
}'

# Pre-load LLM
curl http://localhost:11434/api/generate -d '{
  "model": "llama3.2:3b",
  "prompt": "",
  "keep_alive": -1
}'
```

**Sources:**
- [Ollama Blog - Embedding Models](https://ollama.com/blog/embedding-models)
- [mxbai-embed-large](https://ollama.com/library/mxbai-embed-large)
- [nomic-embed-text](https://ollama.com/library/nomic-embed-text)
- [Local RAG with Ollama and Weaviate](https://weaviate.io/blog/local-rag-with-ollama-and-weaviate)

---

## 5. OLLAMA_NUM_PARALLEL Setting

### What This Controls

`OLLAMA_NUM_PARALLEL` sets the maximum number of parallel requests each model can process simultaneously. This is **per model**, not global.

### Default Behavior

- **Auto-selection**: 4 or 1 based on available memory
- **Memory Impact**: Each parallel slot requires additional context memory

### Impact on Memory

Parallel request processing increases the effective context size:

```
Effective Context = Base Context x OLLAMA_NUM_PARALLEL

Example: 2K context with 4 parallel = 8K context memory allocation
```

### Recommended Values for RAG

| Scenario | Recommended Value | Reasoning |
|----------|-------------------|-----------|
| Single user, low latency | 1-2 | Minimizes memory, fastest individual response |
| Multi-user application | 4-8 | Balance of throughput and memory |
| High concurrency server | 8-16 | Maximum throughput if memory allows |
| Memory constrained | 1 | Minimum memory footprint |

### Configuration Example

```bash
# Low memory system
OLLAMA_NUM_PARALLEL=2 OLLAMA_MAX_LOADED_MODELS=2 ollama serve

# High performance system
OLLAMA_NUM_PARALLEL=8 OLLAMA_MAX_LOADED_MODELS=4 ollama serve
```

**Sources:**
- [Ollama FAQ](https://docs.ollama.com/faq)
- [Collabnix - Does Ollama Use Parallelism](https://collabnix.com/does-ollama-use-parallelism-internally/)
- [GitHub Issue #4170](https://github.com/ollama/ollama/issues/4170)

---

## 6. Alternative: Two Ollama Instances

### Why Consider Two Instances?

1. **Isolation**: Embedding and LLM workloads don't compete for resources
2. **Reliability**: One service can fail without affecting the other
3. **Optimization**: Different configurations for different model types
4. **Scaling**: Can scale embedding and LLM capacity independently

### Can Two Containers Share the Same GPU?

**Yes, but with caveats:**

1. **Default Behavior**: Both containers will see all GPUs by default
2. **Memory Contention**: They share the same VRAM pool
3. **No Hardware Isolation**: Without NVIDIA vGPU or MPS, sharing is at software level

### GPU Sharing Options

#### Option 1: Separate GPUs (Recommended if Available)

```bash
# Container 1 - Embedding on GPU 0
docker run -e CUDA_VISIBLE_DEVICES=0 -p 11434:11434 --name ollama-embed ollama/ollama

# Container 2 - LLM on GPU 1
docker run -e CUDA_VISIBLE_DEVICES=1 -p 11435:11434 --name ollama-llm ollama/ollama
```

#### Option 2: NVIDIA MPS (Multi-Process Service)

NVIDIA MPS allows multiple CUDA applications to share a single GPU more efficiently:

- **Supported**: Volta and newer GPUs
- **Max Clients**: Up to 48 containers (16 for pre-Volta)
- **Resource Control**: `CUDA_MPS_ACTIVE_THREAD_PERCENTAGE` and `CUDA_MPS_PINNED_DEVICE_MEM_LIMIT`
- **Caveat**: Not officially supported by NVIDIA in Docker/Kubernetes

**MPS Setup (Experimental):**

```bash
# Start MPS daemon on host
nvidia-cuda-mps-control -d

# Containers then share GPU via MPS
docker run --gpus all -e CUDA_MPS_PIPE_DIRECTORY=/tmp/nvidia-mps ...
```

#### Option 3: Shared GPU Without MPS

Both containers use the same GPU, relying on CUDA's time-slicing:

```bash
# Both containers use all GPUs (time-shared)
docker run --gpus all -p 11434:11434 --name ollama-embed ollama/ollama
docker run --gpus all -p 11435:11434 --name ollama-llm ollama/ollama
```

**Caveat**: Memory is not partitioned - both containers see full VRAM but total usage cannot exceed physical VRAM.

### Pros and Cons vs Single Instance

| Aspect | Single Instance | Two Instances |
|--------|-----------------|---------------|
| Memory Efficiency | Better (shared memory pool) | Worse (overhead per instance) |
| Isolation | None | Full workload isolation |
| Complexity | Simple | More complex deployment |
| Configuration | Single config | Separate configs per workload |
| Failure Modes | Single point of failure | Independent failures |
| Model Switching | May cause contention | No contention |
| Scaling | Limited | Independent scaling |

**Sources:**
- [Running Multiple Ollama Containers](https://gist.github.com/jrknox1977/15eeb39fd71ae72cf2014a7cbeb9b2e1)
- [NVIDIA MPS Documentation](https://docs.nvidia.com/deploy/mps/index.html)
- [NVIDIA Docker MPS Wiki](https://github.com/NVIDIA/nvidia-docker/wiki/MPS-(EXPERIMENTAL))

---

## 7. Docker Configuration for Multi-Instance

### Prerequisites

1. **NVIDIA Container Toolkit**:

```bash
# Install NVIDIA Container Toolkit
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
sudo apt-get update
sudo apt-get install -y nvidia-container-toolkit
sudo nvidia-ctk runtime configure --runtime=docker
sudo systemctl restart docker
```

2. **Verify GPU Access**:

```bash
docker run --rm --gpus all nvidia/cuda:12.0-base nvidia-smi
```

### Docker Compose: Single Instance (Both Models)

```yaml
version: '3.8'

services:
  ollama:
    image: ollama/ollama:latest
    container_name: ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    environment:
      - OLLAMA_KEEP_ALIVE=-1           # Keep models loaded forever
      - OLLAMA_MAX_LOADED_MODELS=3     # Allow both embedding + LLM + spare
      - OLLAMA_NUM_PARALLEL=4          # Parallel requests per model
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
    restart: unless-stopped

volumes:
  ollama_data:
```

### Docker Compose: Dual Instance (Separate Services)

```yaml
version: '3.8'

services:
  # Embedding Model Service
  ollama-embed:
    image: ollama/ollama:latest
    container_name: ollama-embed
    ports:
      - "11434:11434"
    volumes:
      - ollama_embed_data:/root/.ollama
    environment:
      - OLLAMA_KEEP_ALIVE=-1
      - OLLAMA_MAX_LOADED_MODELS=1
      - OLLAMA_NUM_PARALLEL=8          # Higher parallelism for embeddings
      - CUDA_VISIBLE_DEVICES=0         # Use first GPU (if multi-GPU)
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              device_ids: ['0']        # Specific GPU
              capabilities: [gpu]
    restart: unless-stopped

  # LLM Service
  ollama-llm:
    image: ollama/ollama:latest
    container_name: ollama-llm
    ports:
      - "11435:11434"
    volumes:
      - ollama_llm_data:/root/.ollama
    environment:
      - OLLAMA_KEEP_ALIVE=-1
      - OLLAMA_MAX_LOADED_MODELS=1
      - OLLAMA_NUM_PARALLEL=2          # Lower parallelism for LLM
      - CUDA_VISIBLE_DEVICES=1         # Use second GPU (if multi-GPU)
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              device_ids: ['1']        # Specific GPU
              capabilities: [gpu]
    restart: unless-stopped

volumes:
  ollama_embed_data:
  ollama_llm_data:
```

### Docker Compose: Shared GPU (Single GPU System)

```yaml
version: '3.8'

services:
  # Embedding Model Service
  ollama-embed:
    image: ollama/ollama:latest
    container_name: ollama-embed
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama      # Shared volume to avoid duplicate downloads
    environment:
      - OLLAMA_KEEP_ALIVE=-1
      - OLLAMA_MAX_LOADED_MODELS=1
      - OLLAMA_NUM_PARALLEL=4
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
    restart: unless-stopped

  # LLM Service
  ollama-llm:
    image: ollama/ollama:latest
    container_name: ollama-llm
    ports:
      - "11435:11434"
    volumes:
      - ollama_data:/root/.ollama      # Shared volume
    environment:
      - OLLAMA_KEEP_ALIVE=-1
      - OLLAMA_MAX_LOADED_MODELS=1
      - OLLAMA_NUM_PARALLEL=2
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
    restart: unless-stopped

volumes:
  ollama_data:
```

**Note**: With shared GPU, total VRAM usage from both containers cannot exceed physical VRAM.

**Sources:**
- [Running Ollama with Docker Compose and GPUs](https://dev.to/ajeetraina/running-ollama-with-docker-compose-and-gpus-lkn)
- [Ollama Docker Compose GitHub](https://github.com/mythrantic/ollama-docker)
- [ITsFOSS - Setting Up Ollama With Docker](https://itsfoss.com/ollama-docker/)

---

## 8. Performance Considerations

### Latency Impact of Model Switching

| Scenario | Latency |
|----------|---------|
| Model already loaded | <100ms |
| Model switch (unload + load) | 10-30+ seconds |
| Cold start (first load) | 5-60+ seconds (size dependent) |
| VRAM recovery timeout | +5 seconds worst case |

### Recommendations to Minimize Latency

1. **Pre-load models**: Use `keep_alive: -1` on startup
2. **Avoid model switching**: Use single instance with both models loaded
3. **Size appropriately**: Choose models that fit together in VRAM
4. **Monitor VRAM**: Leave headroom for KV cache growth

### Memory Pressure Scenarios

| Scenario | Behavior |
|----------|----------|
| Both models fit | Both loaded, no switching |
| Only one model fits | Continuous switching, high latency |
| Neither model fits fully | CPU offloading (very slow) |
| Queue full (512 requests) | 503 errors returned |

### GPU Utilization Patterns

- **Embedding Model**: Burst usage, typically high throughput, low latency per request
- **LLM Model**: Sustained usage during generation, sequential token output
- **Combined**: Embedding typically completes before LLM starts (pipeline natural)

### Throughput Considerations

| Configuration | Embedding Throughput | LLM Throughput | Notes |
|---------------|---------------------|----------------|-------|
| Single instance, both loaded | Full | Full | Best if VRAM allows |
| Single instance, switching | Degraded | Degraded | Avoid if possible |
| Dual instance, separate GPUs | Full | Full | Best for multi-GPU |
| Dual instance, shared GPU | ~50% each | ~50% each | Time-sliced |

---

## 9. Recommended Architecture

### Decision Tree

```
Do you have multiple GPUs?
├── Yes: Use dual instances with GPU isolation (Option A)
└── No: Does embedding + LLM fit in VRAM together?
    ├── Yes: Use single instance (Option B)
    └── No: Consider smaller models or CPU for embeddings (Option C)
```

### Option A: Dual Instance with GPU Isolation (Multi-GPU)

**Best for**: Systems with 2+ GPUs

```
GPU 0: ollama-embed (port 11434)
  └── nomic-embed-text or mxbai-embed-large

GPU 1: ollama-llm (port 11435)
  └── llama3.2:3b or larger
```

**Pros**: Full isolation, independent scaling, no contention
**Cons**: More complex deployment, separate model storage

### Option B: Single Instance (Recommended for Single GPU)

**Best for**: Single GPU with sufficient VRAM (8GB+)

```
Single Ollama Instance (port 11434)
  ├── nomic-embed-text (~0.5 GB)
  └── llama3.2:3b (~4 GB)

Total: ~4.5 GB + overhead
```

**Configuration**:
```bash
OLLAMA_KEEP_ALIVE=-1
OLLAMA_MAX_LOADED_MODELS=3
OLLAMA_NUM_PARALLEL=4
```

**Pros**: Simple, memory efficient, no duplication
**Cons**: Shared resources, potential contention

### Option C: Hybrid (CPU Embeddings, GPU LLM)

**Best for**: Limited VRAM, high embedding volume

```
CPU: Embedding model
GPU: LLM only

# Set CPU-only for specific model
ollama run nomic-embed-text --num-gpu 0
```

**Pros**: Frees GPU entirely for LLM
**Cons**: Slower embeddings (but embeddings are less latency-sensitive)

### Fallback Strategies

1. **Graceful Degradation**: If VRAM full, queue requests instead of failing
2. **Health Checks**: Monitor `ollama ps` for loaded models
3. **Auto-Recovery**: Restart container on model loading failures
4. **Load Shedding**: Return cached responses during overload

---

## 10. Complete Examples

### Example 1: MCP Server Configuration (Single Instance)

**Environment Variables** (`.env`):
```bash
OLLAMA_HOST=http://localhost:11434
OLLAMA_KEEP_ALIVE=-1
OLLAMA_MAX_LOADED_MODELS=3
OLLAMA_NUM_PARALLEL=4
OLLAMA_MAX_QUEUE=512
```

**Docker Compose** (`docker-compose.yml`):
```yaml
version: '3.8'

services:
  ollama:
    image: ollama/ollama:latest
    container_name: ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    environment:
      - OLLAMA_KEEP_ALIVE=-1
      - OLLAMA_MAX_LOADED_MODELS=3
      - OLLAMA_NUM_PARALLEL=4
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:11434/api/tags"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  ollama_data:
```

**Startup Script** (`init-models.sh`):
```bash
#!/bin/bash

# Wait for Ollama to be ready
until curl -s http://localhost:11434/api/tags > /dev/null; do
  echo "Waiting for Ollama..."
  sleep 2
done

# Pull models if not present
ollama pull nomic-embed-text
ollama pull llama3.2:3b

# Pre-load models with keep_alive=-1
echo "Pre-loading embedding model..."
curl -s http://localhost:11434/api/embeddings -d '{
  "model": "nomic-embed-text",
  "prompt": "warmup",
  "keep_alive": -1
}' > /dev/null

echo "Pre-loading LLM..."
curl -s http://localhost:11434/api/generate -d '{
  "model": "llama3.2:3b",
  "prompt": "",
  "keep_alive": -1
}' > /dev/null

echo "Models loaded. Verifying..."
ollama ps
```

### Example 2: Dual Instance for Production

**Docker Compose** (`docker-compose.prod.yml`):
```yaml
version: '3.8'

services:
  ollama-embed:
    image: ollama/ollama:latest
    container_name: ollama-embed
    hostname: ollama-embed
    ports:
      - "11434:11434"
    volumes:
      - ollama_embed:/root/.ollama
    environment:
      - OLLAMA_KEEP_ALIVE=-1
      - OLLAMA_MAX_LOADED_MODELS=1
      - OLLAMA_NUM_PARALLEL=8
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              device_ids: ['0']
              capabilities: [gpu]
        limits:
          memory: 4G
    restart: unless-stopped
    networks:
      - ollama-network

  ollama-llm:
    image: ollama/ollama:latest
    container_name: ollama-llm
    hostname: ollama-llm
    ports:
      - "11435:11434"
    volumes:
      - ollama_llm:/root/.ollama
    environment:
      - OLLAMA_KEEP_ALIVE=-1
      - OLLAMA_MAX_LOADED_MODELS=1
      - OLLAMA_NUM_PARALLEL=4
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              device_ids: ['1']
              capabilities: [gpu]
        limits:
          memory: 16G
    restart: unless-stopped
    networks:
      - ollama-network

volumes:
  ollama_embed:
  ollama_llm:

networks:
  ollama-network:
    driver: bridge
```

### Example 3: MCP Server Code Pattern (C#)

```csharp
public class OllamaConfig
{
    public string EmbeddingEndpoint { get; set; } = "http://localhost:11434";
    public string LlmEndpoint { get; set; } = "http://localhost:11434"; // Same for single instance
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string LlmModel { get; set; } = "llama3.2:3b";
    public int KeepAlive { get; set; } = -1; // Forever
}

public class OllamaService
{
    private readonly OllamaConfig _config;
    private readonly HttpClient _httpClient;

    public async Task<float[]> GetEmbeddingsAsync(string text)
    {
        var request = new
        {
            model = _config.EmbeddingModel,
            prompt = text,
            keep_alive = _config.KeepAlive
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_config.EmbeddingEndpoint}/api/embeddings",
            request);

        // Parse and return embeddings
    }

    public async Task<string> GenerateAsync(string prompt, string context)
    {
        var request = new
        {
            model = _config.LlmModel,
            prompt = $"Context: {context}\n\nQuestion: {prompt}",
            keep_alive = _config.KeepAlive,
            stream = false
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_config.LlmEndpoint}/api/generate",
            request);

        // Parse and return response
    }
}
```

### Example 4: Health Check and Monitoring

```bash
#!/bin/bash
# health-check.sh

EMBED_URL="http://localhost:11434"
LLM_URL="http://localhost:11435"  # Change to 11434 for single instance

# Check embedding service
embed_status=$(curl -s -o /dev/null -w "%{http_code}" "$EMBED_URL/api/tags")
if [ "$embed_status" != "200" ]; then
    echo "ERROR: Embedding service not responding"
    exit 1
fi

# Check LLM service
llm_status=$(curl -s -o /dev/null -w "%{http_code}" "$LLM_URL/api/tags")
if [ "$llm_status" != "200" ]; then
    echo "ERROR: LLM service not responding"
    exit 1
fi

# Check models are loaded
embed_models=$(curl -s "$EMBED_URL/api/ps" | jq -r '.models[].name')
llm_models=$(curl -s "$LLM_URL/api/ps" | jq -r '.models[].name')

echo "Embedding models loaded: $embed_models"
echo "LLM models loaded: $llm_models"

# Check GPU memory
nvidia-smi --query-gpu=memory.used,memory.total --format=csv,noheader,nounits
```

---

## Summary

### Key Takeaways

1. **Ollama supports multiple models** simultaneously via `OLLAMA_MAX_LOADED_MODELS`
2. **Use `keep_alive: -1`** to prevent model unloading and eliminate switching latency
3. **Single instance is simpler** and more memory-efficient for most RAG use cases
4. **Dual instances provide isolation** but require more resources and complexity
5. **VRAM is the primary constraint** - ensure both models fit together
6. **Embedding models are small** (~0.5-1.2 GB) and leave plenty of room for LLMs

### Recommended Configuration for RAG MCP Server

For a typical single-GPU setup with 8GB+ VRAM:

```yaml
environment:
  - OLLAMA_KEEP_ALIVE=-1          # Never unload
  - OLLAMA_MAX_LOADED_MODELS=3    # Embedding + LLM + spare
  - OLLAMA_NUM_PARALLEL=4         # Concurrent requests
```

Models:
- **Embedding**: `nomic-embed-text` (0.5 GB, 8K context, fast)
- **LLM**: `llama3.2:3b` (4 GB) or `llama3.1:8b` (6-7 GB) if VRAM allows

---

## Sources

### Official Documentation
- [Ollama FAQ](https://docs.ollama.com/faq)
- [Ollama Docker Documentation](https://docs.ollama.com/docker)
- [Ollama Blog - Embedding Models](https://ollama.com/blog/embedding-models)

### Model Information
- [mxbai-embed-large](https://ollama.com/library/mxbai-embed-large)
- [nomic-embed-text](https://ollama.com/library/nomic-embed-text)

### Technical Deep Dives
- [DeepWiki - Memory Management and GPU Allocation](https://deepwiki.com/ollama/ollama/5.4-memory-management-and-gpu-allocation)
- [How Ollama Handles Parallel Requests](https://www.glukhov.org/post/2025/05/how-ollama-handles-parallel-requests/)
- [Collabnix - Ollama Parallelism](https://collabnix.com/does-ollama-use-parallelism-internally/)

### GitHub Issues and Discussions
- [GitHub Issue #2109 - Multiple Models](https://github.com/ollama/ollama/issues/2109)
- [GitHub Issue #5272 - keep_alive](https://github.com/ollama/ollama/issues/5272)
- [GitHub Issue #7130 - VRAM Timeout](https://github.com/ollama/ollama/issues/7130)

### Docker and GPU Configuration
- [Running Ollama with Docker Compose and GPUs](https://dev.to/ajeetraina/running-ollama-with-docker-compose-and-gpus-lkn)
- [Running Multiple Ollama Containers](https://gist.github.com/jrknox1977/15eeb39fd71ae72cf2014a7cbeb9b2e1)
- [NVIDIA MPS Documentation](https://docs.nvidia.com/deploy/mps/index.html)
- [ITsFOSS - Setting Up Ollama With Docker](https://itsfoss.com/ollama-docker/)

### RAG Implementation
- [Local RAG with Ollama and Weaviate](https://weaviate.io/blog/local-rag-with-ollama-and-weaviate)
- [Build RAG with LangChain and Ollama](https://devblogs.microsoft.com/cosmosdb/build-a-rag-application-with-langchain-and-local-llms-powered-by-ollama/)
