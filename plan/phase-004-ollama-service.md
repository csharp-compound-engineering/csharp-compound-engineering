# Phase 004: Ollama Service Configuration

> **Status**: [PLANNED]
> **Category**: Infrastructure Setup
> **Estimated Effort**: M
> **Prerequisites**: Phase 002

---

## Spec References

- [infrastructure.md - Ollama](../spec/infrastructure.md#ollama)
- [infrastructure.md - Ollama Configuration](../spec/infrastructure.md#ollama-configuration)
- [infrastructure.md - Port Assignments](../spec/infrastructure.md#port-assignments)
- [mcp-server/ollama-integration.md](../spec/mcp-server/ollama-integration.md)
- [research/ollama-docker-gpu-research.md](../research/ollama-docker-gpu-research.md)
- [research/ollama-multi-model-research.md](../research/ollama-multi-model-research.md)

---

## Objectives

1. Add Ollama service definition to docker-compose.yml
2. Implement GPU support configuration for NVIDIA, AMD, and CPU-only modes
3. Create ollama-config.json schema and default configuration
4. Configure model auto-pull for required models (mxbai-embed-large, mistral)
5. Set up volume mount for model persistence
6. Integrate Ollama health check into startup flow
7. Document Apple Silicon detection and native Ollama assumption

---

## Acceptance Criteria

- [ ] docker-compose.yml includes ollama service with correct image (ollama/ollama:latest)
- [ ] Ollama container exposes port 11435 (mapped from internal 11434)
- [ ] Port binding is localhost-only (127.0.0.1:11435:11434)
- [ ] Volume mount configured for ~/.claude/.csharp-compounding-docs/ollama/models:/root/.ollama
- [ ] ollama-config.json template created with GPU and model configuration
- [ ] NVIDIA GPU configuration injected via deploy.resources.reservations.devices
- [ ] AMD GPU configuration uses /dev/kfd and /dev/dri device mappings
- [ ] Container name follows convention: csharp-compounding-docs-ollama
- [ ] Health check implemented using /api/tags endpoint
- [ ] Model auto-pull documented with required models list
- [ ] Apple Silicon detection documented with native Ollama fallback

---

## Implementation Notes

### 1. Ollama Service Definition in docker-compose.yml

Add the following service to the existing docker-compose.yml:

```yaml
services:
  ollama:
    image: ollama/ollama:latest
    container_name: csharp-compounding-docs-ollama
    volumes:
      - ~/.claude/.csharp-compounding-docs/ollama/models:/root/.ollama
    ports:
      - "127.0.0.1:11435:11434"
    environment:
      - OLLAMA_KEEP_ALIVE=-1
      - OLLAMA_MAX_LOADED_MODELS=3
      - OLLAMA_NUM_PARALLEL=2
    restart: unless-stopped
    networks:
      - compounding-docs-network
    # GPU configuration injected by launcher script based on ollama-config.json
```

### 2. ollama-config.json Structure

Create template at `~/.claude/.csharp-compounding-docs/ollama-config.json`:

```json
{
  "generation_model": "mistral",
  "gpu": {
    "enabled": false,
    "type": null
  },
  "environment": {
    "OLLAMA_KEEP_ALIVE": "-1",
    "OLLAMA_MAX_LOADED_MODELS": "3",
    "OLLAMA_NUM_PARALLEL": "2"
  }
}
```

**Configuration Fields**:

| Field | Type | Description |
|-------|------|-------------|
| `generation_model` | string | LLM model for RAG synthesis (default: mistral) |
| `gpu.enabled` | boolean | Whether GPU acceleration is enabled |
| `gpu.type` | string | `null`, `"nvidia"`, or `"amd"` |
| `environment` | object | Ollama environment variable overrides |

**Note**: The embedding model (`mxbai-embed-large`) is fixed and not configurable.

### 3. GPU Configuration Injection

The launcher script (Phase 006) reads ollama-config.json and injects GPU configuration into docker-compose.yml.

#### NVIDIA GPU Configuration

When `gpu.type` is `"nvidia"`:

```yaml
services:
  ollama:
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
```

**Requirements**:
- NVIDIA Container Toolkit installed on host
- Docker >= 19.03
- NVIDIA drivers >= 418.81.07

#### AMD GPU Configuration

When `gpu.type` is `"amd"`:

```yaml
services:
  ollama:
    image: ollama/ollama:rocm  # Use ROCm image
    devices:
      - /dev/kfd
      - /dev/dri
    group_add:
      - video
      - render
```

**Requirements**:
- AMD GPU with ROCm support (RDNA2/RDNA3 recommended)
- ROCm 5.0+ drivers on host

#### CPU-Only Configuration

When `gpu.enabled` is `false` (default):

```yaml
services:
  ollama:
    deploy:
      resources:
        limits:
          cpus: '4'
          memory: 8G
```

### 4. Apple Silicon Detection

Apple Silicon (macOS ARM64) cannot use GPU acceleration in Docker containers. The launcher script must:

1. Detect if running on macOS with Apple Silicon
2. Skip Ollama Docker container entirely
3. Assume Ollama is running natively at localhost:11434
4. Provide clear error if native Ollama is not running

**Detection Logic** (PowerShell):

```powershell
$IsAppleSilicon = ($IsMacOS -and [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64')

if ($IsAppleSilicon) {
    Write-Host "Apple Silicon detected. Using native Ollama at localhost:11434"
    # Skip Docker Ollama, use native
}
```

### 5. Health Check Configuration

```yaml
services:
  ollama:
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:11434/api/tags || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 5
      start_period: 120s  # Models may need time to load
```

**Note**: The official Ollama image includes curl as of recent versions. If not available, use:

```yaml
healthcheck:
  test: ["CMD-SHELL", "wget -q --spider http://localhost:11434/api/tags || exit 1"]
```

### 6. Model Auto-Pull Configuration

Required models (pulled automatically on first use):

| Model | Purpose | Size | VRAM Usage |
|-------|---------|------|------------|
| `mxbai-embed-large` | Embedding generation (1024 dimensions) | ~1.2 GB | ~1.2 GB |
| `mistral` | RAG synthesis (default) | ~4.1 GB | ~4-5 GB |

**Pre-pull Script** (optional, for faster first-run):

```bash
#!/bin/bash
# Pull models after container starts
docker exec csharp-compounding-docs-ollama ollama pull mxbai-embed-large
docker exec csharp-compounding-docs-ollama ollama pull mistral
```

### 7. Environment Variables Reference

| Variable | Default | Description |
|----------|---------|-------------|
| `OLLAMA_KEEP_ALIVE` | `-1` | Keep models loaded forever (no auto-unload) |
| `OLLAMA_MAX_LOADED_MODELS` | `3` | Allow embedding + LLM + spare |
| `OLLAMA_NUM_PARALLEL` | `2` | Concurrent requests per model |
| `OLLAMA_MAX_QUEUE` | `512` | Max queued requests before 503 |
| `OLLAMA_DEBUG` | `0` | Enable debug logging (1 for enabled) |

### 8. Port Assignment

| Service | Internal Port | Exposed Port | Binding |
|---------|---------------|--------------|---------|
| Ollama | 11434 | 11435 | 127.0.0.1 only |

Non-standard port (11435) avoids conflicts with native Ollama installations (11434).

### 9. Volume Mount

```yaml
volumes:
  - ~/.claude/.csharp-compounding-docs/ollama/models:/root/.ollama
```

**Purpose**: Persist downloaded models across container restarts and upgrades.

**Contents**:
- `models/manifests/` - JSON model configurations
- `models/blobs/` - Content-addressed model weights

### 10. Network Configuration

Ollama joins the same Docker network as PostgreSQL:

```yaml
networks:
  compounding-docs-network:
    driver: bridge
```

This allows internal communication between services without port exposure.

---

## Dependencies

### Depends On

- **Phase 002**: Docker Compose Stack Creation - Base docker-compose.yml must exist before adding Ollama service

### Blocks

- **Phase 005**: PostgreSQL + Liquibase Service - Ollama must be defined before completing full infrastructure
- **Phase 006**: PowerShell Launcher Script - Launcher needs Ollama config to inject GPU settings
- **Phase 030+**: MCP Server Ollama Integration - Server needs running Ollama service

---

## Testing Verification

After implementation, verify with:

```bash
# 1. Start stack without GPU
docker compose -p csharp-compounding-docs up -d ollama

# 2. Check container running
docker ps | grep ollama

# 3. Verify health
curl http://localhost:11435/api/tags

# 4. Test model pull
docker exec csharp-compounding-docs-ollama ollama pull mxbai-embed-large

# 5. Test embedding endpoint
curl http://localhost:11435/api/embeddings -d '{
  "model": "mxbai-embed-large",
  "prompt": "test embedding"
}'
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `docker/docker-compose.yml` | Modify | Add ollama service definition |
| `docker/docker-compose.nvidia.yml` | Create | NVIDIA GPU overlay |
| `docker/docker-compose.amd.yml` | Create | AMD GPU overlay |
| `templates/ollama-config.json` | Create | Default Ollama configuration template |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| GPU drivers not installed | Fallback to CPU-only with clear warning |
| Port 11435 in use | Document user can modify in ollama-config.json |
| Model download timeout | Use generous start_period in health check |
| Apple Silicon confusion | Clear error message directing to native Ollama |
| Memory pressure | Document minimum requirements (8GB recommended) |
