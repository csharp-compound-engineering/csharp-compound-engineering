# Ollama Docker GPU Research Report

**Research Date**: January 2026
**Context**: Running Ollama in Docker with GPU acceleration for an MCP server RAG implementation alongside PostgreSQL.

---

## Table of Contents

1. [Ollama Docker Image](#1-ollama-docker-image)
2. [Basic Docker Run Commands](#2-basic-docker-run-commands)
3. [GPU Passthrough - NVIDIA](#3-gpu-passthrough---nvidia)
4. [GPU Passthrough - AMD (ROCm)](#4-gpu-passthrough---amd-rocm)
5. [GPU Passthrough - Apple Silicon](#5-gpu-passthrough---apple-silicon)
6. [Docker Compose Configuration](#6-docker-compose-configuration)
7. [Model Management in Docker](#7-model-management-in-docker)
8. [Multi-GPU Configuration](#8-multi-gpu-configuration)
9. [Performance Tuning](#9-performance-tuning)
10. [Health Checks and Monitoring](#10-health-checks-and-monitoring)
11. [Docker Compose Examples](#11-docker-compose-examples)
12. [Troubleshooting](#12-troubleshooting)
13. [Security Considerations](#13-security-considerations)

---

## 1. Ollama Docker Image

### Official Image

The official Ollama Docker image is available at [ollama/ollama on Docker Hub](https://hub.docker.com/r/ollama/ollama).

### Image Tags and Variants

| Tag | Description | Use Case |
|-----|-------------|----------|
| `latest` | Standard image with CUDA support | NVIDIA GPUs, CPU |
| `rocm` | AMD ROCm support | AMD GPUs |
| Specific versions (e.g., `0.7.0`) | Pinned versions | Production stability |

### Image Architecture

- Built for both `linux/amd64` and `linux/arm64` architectures
- GPU acceleration libraries located in `/usr/lib/ollama`:
  - `cuda_v12/` - CUDA 12.x backend (`libggml-cuda.so`, `libcudart.so.12`, `libcublas.so.12`, `libcublasLt.so.12`)
  - `cuda_v13/` - CUDA 13.x support
  - `rocm/` - AMD ROCm backend

### What's Included

- Ollama server binary
- GPU acceleration libraries (CUDA, ROCm)
- Model storage infrastructure
- REST API server (port 11434)

---

## 2. Basic Docker Run Commands

### CPU-Only (Basic)

```bash
docker run -d \
  -v ollama:/root/.ollama \
  -p 11434:11434 \
  --name ollama \
  ollama/ollama
```

### Key Parameters Explained

| Parameter | Description |
|-----------|-------------|
| `-d` | Run in detached mode (background) |
| `-v ollama:/root/.ollama` | Named volume for model persistence |
| `-p 11434:11434` | Port mapping for API access |
| `--name ollama` | Container name for easy reference |
| `--restart always` | Auto-restart policy |

### Port Mapping

- **Default Port**: 11434
- **API Endpoint**: `http://localhost:11434`
- **Binding**: Use `-p 127.0.0.1:11434:11434` to restrict to localhost only

### Running Models

After starting the container:

```bash
# Pull a model
docker exec -it ollama ollama pull llama3.2

# Run interactively
docker exec -it ollama ollama run llama3.2

# List models
docker exec -it ollama ollama list
```

---

## 3. GPU Passthrough - NVIDIA

### Prerequisites

1. **NVIDIA GPU** with Architecture >= Kepler (compute capability 3.0+)
2. **NVIDIA Linux drivers** >= 418.81.07
3. **Docker** >= 19.03
4. **NVIDIA Container Toolkit** installed

### Installing NVIDIA Container Toolkit

#### Ubuntu/Debian

```bash
# Add NVIDIA package repository
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg

curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | \
  sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | \
  sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list

# Install the toolkit
sudo apt-get update
sudo apt-get install -y nvidia-container-toolkit

# Configure Docker runtime
sudo nvidia-ctk runtime configure --runtime=docker

# Restart Docker
sudo systemctl restart docker
```

### Docker Configuration

The `nvidia-ctk` command modifies `/etc/docker/daemon.json`:

```json
{
  "runtimes": {
    "nvidia": {
      "path": "nvidia-container-runtime",
      "runtimeArgs": []
    }
  }
}
```

### Running Ollama with NVIDIA GPU

```bash
docker run -d \
  --gpus all \
  -v ollama:/root/.ollama \
  -p 11434:11434 \
  --name ollama \
  ollama/ollama
```

### GPU Selection Options

| Flag | Description |
|------|-------------|
| `--gpus all` | Use all available GPUs |
| `--gpus 1` | Use first GPU only |
| `--gpus '"device=0,1"'` | Use specific GPUs by ID |
| `--gpus '"device=GPU-UUID"'` | Use GPU by UUID |

### Environment Variables for GPU Control

```bash
# Limit to specific GPUs
docker run -d \
  --gpus all \
  -e CUDA_VISIBLE_DEVICES=0,1 \
  -v ollama:/root/.ollama \
  -p 11434:11434 \
  --name ollama \
  ollama/ollama
```

### Verifying GPU Access

```bash
# Check container logs for GPU detection
docker logs ollama | grep -i gpu

# Look for messages like:
# msg="inference compute" id=... library=cuda variant=v12 compute=...

# Verify from host
docker run --gpus all nvidia/cuda:12.4.0-base-ubuntu22.04 nvidia-smi
```

### CUDA Version Compatibility

| CUDA Version | Driver Version (Minimum) |
|--------------|-------------------------|
| CUDA 12.x | >= 525.60.13 |
| CUDA 11.x | >= 450.80.02 |

---

## 4. GPU Passthrough - AMD (ROCm)

### Prerequisites

- AMD GPU with ROCm support (RDNA2/RDNA3 recommended)
- ROCm 5.0+ drivers installed on host
- Minimum 16+ GB VRAM recommended

### Basic Docker Run Command

```bash
docker run -d \
  --device /dev/kfd \
  --device /dev/dri \
  -v ollama:/root/.ollama \
  -p 11434:11434 \
  --name ollama \
  ollama/ollama:rocm
```

### Device Mapping

| Device | Purpose |
|--------|---------|
| `/dev/kfd` | Kernel Fusion Driver (compute) |
| `/dev/dri` | Direct Rendering Infrastructure |

### GPU Override for Specific Architectures

Some AMD GPUs require version overrides:

```bash
docker run -d \
  --device /dev/kfd \
  --device /dev/dri \
  -e HSA_OVERRIDE_GFX_VERSION=10.3.0 \
  -v ollama:/root/.ollama \
  -p 11434:11434 \
  --name ollama \
  ollama/ollama:rocm
```

Common `HSA_OVERRIDE_GFX_VERSION` values:
- RX 6600/6700/6800/6900: `10.3.0`
- RX 7800/7900: `11.0.0`

### GPU Selection for AMD

```bash
# Select specific AMD GPU
docker run -d \
  --device /dev/kfd \
  --device /dev/dri \
  -e HIP_VISIBLE_DEVICES=0 \
  -e ROCR_VISIBLE_DEVICES=0 \
  -v ollama:/root/.ollama \
  -p 11434:11434 \
  --name ollama \
  ollama/ollama:rocm
```

### Permissions

ROCm requires elevated privileges. Either:
1. Add user to `render` group: `sudo usermod -aG render $USER`
2. Run container with `--privileged` flag (less secure)

---

## 5. GPU Passthrough - Apple Silicon

### Critical Limitation

**GPU acceleration is NOT available for Docker on macOS.**

Docker Desktop on Mac does not expose the Apple GPU to container runtime. It only exposes an ARM CPU (or virtual x86 CPU via Rosetta emulation).

### Technical Explanation

- All virtualization on Apple Silicon (Docker, Podman, Parallels) uses Apple's `Hypervisor.framework`
- `Hypervisor.framework` does not provide virtual GPU support
- Apple GPUs use Metal Performance Shaders API, not widely supported like CUDA

### Performance Impact

Running Ollama in Docker on Mac:
- **Native Ollama**: Up to 5-6x faster inference
- **Docker Ollama**: CPU-only, significantly slower

### Recommendations

| Approach | GPU Support | Performance |
|----------|-------------|-------------|
| Native Ollama installation | Yes (Metal) | Best |
| Docker with Ollama | No (CPU only) | Poor |
| Podman with Vulkan (experimental) | Limited | Experimental |

### For Mac Development

If you need Docker for your stack but want Ollama performance:

1. Run Ollama natively on Mac: `brew install ollama`
2. Run other services (PostgreSQL, etc.) in Docker
3. Configure your MCP server to connect to native Ollama at `localhost:11434`

---

## 6. Docker Compose Configuration

### GPU Configuration Syntax

Docker Compose uses the `deploy.resources.reservations.devices` specification:

```yaml
services:
  ollama:
    image: ollama/ollama:latest
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
```

### Device Properties

| Property | Description | Values |
|----------|-------------|--------|
| `driver` | GPU driver | `nvidia` |
| `count` | Number of GPUs | `all`, `1`, `2`, etc. |
| `capabilities` | Required capabilities | `[gpu]`, `[gpu, compute]` |
| `device_ids` | Specific GPU IDs | `['0', '1']`, `['GPU-UUID']` |

### Volume Definitions

```yaml
volumes:
  ollama_data:
    driver: local
```

### Health Check Configuration

```yaml
services:
  ollama:
    image: ollama/ollama:latest
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:11434/api/tags"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s
```

**Note**: The official Ollama image doesn't include `curl`. Options:
1. Use custom image with curl installed
2. Use `wget` if available
3. Use a sidecar container for health checks

### Network Configuration

```yaml
networks:
  app_network:
    driver: bridge

services:
  ollama:
    networks:
      - app_network
```

### Restart Policies

| Policy | Description |
|--------|-------------|
| `no` | Never restart |
| `always` | Always restart |
| `on-failure` | Restart on non-zero exit |
| `unless-stopped` | Restart unless explicitly stopped |

---

## 7. Model Management in Docker

### Model Storage Location

- **Container Path**: `/root/.ollama`
- **Subdirectories**:
  - `models/manifests/` - JSON model configurations
  - `models/blobs/` - Content-addressed model weights

### Persisting Models with Volumes

```yaml
services:
  ollama:
    volumes:
      - ollama_data:/root/.ollama

volumes:
  ollama_data:
```

### Pulling Models

```bash
# From host
docker exec ollama ollama pull llama3.2

# Multiple models
docker exec ollama ollama pull llama3.2
docker exec ollama ollama pull mistral
docker exec ollama ollama pull codellama
```

### Pre-loading Models in Docker Image

#### Dockerfile Approach

```dockerfile
FROM ollama/ollama:latest

# Start server, pull model, stop server
RUN ollama serve & \
    sleep 10 && \
    ollama pull llama3.2 && \
    pkill ollama

# Models are now baked into image
```

#### Multi-stage Build (More Robust)

```dockerfile
# Stage 1: Download models
FROM ollama/ollama:latest AS downloader

RUN ollama serve & \
    sleep 10 && \
    ollama pull llama3.2 && \
    ollama pull mistral && \
    pkill ollama

# Stage 2: Final image
FROM ollama/ollama:latest
COPY --from=downloader /root/.ollama /root/.ollama
```

### Pre-loading via API (Runtime)

Send an empty request to preload a model into memory:

```bash
curl http://localhost:11434/api/generate -d '{
  "model": "llama3.2",
  "prompt": ""
}'
```

### Upgrading Without Losing Models

```bash
# Models are in volume, safe to recreate container
docker pull ollama/ollama:latest
docker stop ollama
docker rm ollama
docker run -d --gpus all -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama
```

---

## 8. Multi-GPU Configuration

### Automatic Distribution

Ollama automatically distributes model layers across available GPUs when using `--gpus all` or `count: all`.

### GPU Selection

#### NVIDIA

```bash
# Environment variable method
docker run -d \
  --gpus all \
  -e CUDA_VISIBLE_DEVICES=0,1 \
  -v ollama:/root/.ollama \
  -p 11434:11434 \
  --name ollama \
  ollama/ollama
```

#### AMD

```bash
docker run -d \
  --device /dev/kfd \
  --device /dev/dri \
  -e ROCR_VISIBLE_DEVICES=0,1 \
  -v ollama:/root/.ollama \
  -p 11434:11434 \
  --name ollama \
  ollama/ollama:rocm
```

### Docker Compose with Specific GPUs

```yaml
services:
  ollama:
    image: ollama/ollama:latest
    environment:
      - CUDA_VISIBLE_DEVICES=0,1
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              device_ids: ['0', '1']
              capabilities: [gpu]
```

### Load Balancing with Multiple Ollama Instances

For true load balancing, run multiple Ollama containers with nginx:

```yaml
services:
  ollama-1:
    image: ollama/ollama:latest
    environment:
      - CUDA_VISIBLE_DEVICES=0
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              device_ids: ['0']
              capabilities: [gpu]
    volumes:
      - ollama_models:/root/.ollama

  ollama-2:
    image: ollama/ollama:latest
    environment:
      - CUDA_VISIBLE_DEVICES=1
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              device_ids: ['1']
              capabilities: [gpu]
    volumes:
      - ollama_models:/root/.ollama

  nginx:
    image: nginx:alpine
    ports:
      - "11434:80"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
    depends_on:
      - ollama-1
      - ollama-2
```

**nginx.conf**:
```nginx
upstream ollama_backend {
    server ollama-1:11434;
    server ollama-2:11434;
}

server {
    listen 80;
    location / {
        proxy_pass http://ollama_backend;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

### Known Limitations

- Models are loaded to the fastest/first available GPU by default
- A model fitting in one GPU won't automatically split across multiple GPUs
- Manual layer distribution requires `num_gpu` parameter in API calls

---

## 9. Performance Tuning

### Key Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `OLLAMA_NUM_PARALLEL` | Max parallel requests per model | Auto (1-4 based on memory) |
| `OLLAMA_MAX_LOADED_MODELS` | Max concurrent models in memory | 3 x GPU count (or 3 for CPU) |
| `OLLAMA_MAX_QUEUE` | Max queued requests before 503 | 512 |
| `OLLAMA_KEEP_ALIVE` | Time to keep model in memory | 5m |
| `OLLAMA_GPU_OVERHEAD` | Reserved VRAM per GPU (bytes) | 0 |

### Docker Compose with Tuning

```yaml
services:
  ollama:
    image: ollama/ollama:latest
    environment:
      - OLLAMA_NUM_PARALLEL=4
      - OLLAMA_MAX_LOADED_MODELS=2
      - OLLAMA_MAX_QUEUE=256
      - OLLAMA_KEEP_ALIVE=10m
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
```

### Memory Considerations

- **Parallel requests**: Each parallel request multiplies context size
  - Example: 2K context + 4 parallel = 8K effective context
- **GPU VRAM**: Models must fully fit in VRAM for concurrent loading
- **System RAM**: Used for CPU inference and model loading

### Optimizing for RAG Workloads

For RAG (your use case), consider:

```yaml
environment:
  # Multiple parallel requests for embedding/retrieval
  - OLLAMA_NUM_PARALLEL=4
  # Keep embedding model always loaded
  - OLLAMA_KEEP_ALIVE=24h
  # Allow main model + embedding model
  - OLLAMA_MAX_LOADED_MODELS=2
```

### Container Resource Limits

```yaml
services:
  ollama:
    deploy:
      resources:
        limits:
          memory: 32G
        reservations:
          memory: 16G
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
```

---

## 10. Health Checks and Monitoring

### Available Endpoints

| Endpoint | Purpose | Response |
|----------|---------|----------|
| `/` | Root/ping | "Ollama is running" |
| `/api/tags` | List models | JSON with models |
| `/api/version` | Version info | Version string |

### Docker Health Check

```yaml
services:
  ollama:
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:11434/api/tags || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s
```

### Without curl (Using wget)

```yaml
healthcheck:
  test: ["CMD-SHELL", "wget -q --spider http://localhost:11434/api/tags || exit 1"]
  interval: 30s
  timeout: 10s
  retries: 3
```

### Custom Health Check Image

Create a Dockerfile:

```dockerfile
FROM ollama/ollama:latest
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
```

### Monitoring GPU Usage

```bash
# From host
nvidia-smi -l 1  # Update every second

# Watch specific metrics
nvidia-smi --query-gpu=utilization.gpu,memory.used,memory.total --format=csv -l 1
```

### Container Logging

```yaml
services:
  ollama:
    logging:
      driver: "json-file"
      options:
        max-size: "100m"
        max-file: "3"
    environment:
      - OLLAMA_DEBUG=1  # Enable debug logging
```

---

## 11. Docker Compose Examples

### Complete Example: Ollama + PostgreSQL (NVIDIA GPU)

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
      - OLLAMA_NUM_PARALLEL=4
      - OLLAMA_MAX_LOADED_MODELS=2
      - OLLAMA_KEEP_ALIVE=10m
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:11434/ || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s
    restart: unless-stopped
    networks:
      - app_network

  postgres:
    image: pgvector/pgvector:pg16
    container_name: postgres
    ports:
      - "5432:5432"
    environment:
      - POSTGRES_USER=rag_user
      - POSTGRES_PASSWORD=secure_password
      - POSTGRES_DB=rag_database
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U rag_user -d rag_database"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    networks:
      - app_network

networks:
  app_network:
    driver: bridge

volumes:
  ollama_data:
  postgres_data:
```

### AMD GPU Configuration

```yaml
version: '3.8'

services:
  ollama:
    image: ollama/ollama:rocm
    container_name: ollama
    ports:
      - "11434:11434"
    devices:
      - /dev/kfd:/dev/kfd
      - /dev/dri:/dev/dri
    volumes:
      - ollama_data:/root/.ollama
    environment:
      - HSA_OVERRIDE_GFX_VERSION=10.3.0  # Adjust for your GPU
      - OLLAMA_NUM_PARALLEL=2
    group_add:
      - render
    restart: unless-stopped
    networks:
      - app_network

  postgres:
    image: pgvector/pgvector:pg16
    container_name: postgres
    ports:
      - "5432:5432"
    environment:
      - POSTGRES_USER=rag_user
      - POSTGRES_PASSWORD=secure_password
      - POSTGRES_DB=rag_database
    volumes:
      - postgres_data:/var/lib/postgresql/data
    restart: unless-stopped
    networks:
      - app_network

networks:
  app_network:
    driver: bridge

volumes:
  ollama_data:
  postgres_data:
```

### CPU-Only Fallback

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
      - OLLAMA_NUM_PARALLEL=2
      - OLLAMA_MAX_LOADED_MODELS=1
    deploy:
      resources:
        limits:
          cpus: '4'
          memory: 16G
    restart: unless-stopped
    networks:
      - app_network

  postgres:
    image: pgvector/pgvector:pg16
    container_name: postgres
    ports:
      - "5432:5432"
    environment:
      - POSTGRES_USER=rag_user
      - POSTGRES_PASSWORD=secure_password
      - POSTGRES_DB=rag_database
    volumes:
      - postgres_data:/var/lib/postgresql/data
    restart: unless-stopped
    networks:
      - app_network

networks:
  app_network:
    driver: bridge

volumes:
  ollama_data:
  postgres_data:
```

### Development vs Production

**Development** (`docker-compose.dev.yml`):

```yaml
version: '3.8'

services:
  ollama:
    image: ollama/ollama:latest
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    environment:
      - OLLAMA_DEBUG=1
    # No GPU config - CPU only for dev
    restart: "no"
```

**Production** (`docker-compose.prod.yml`):

```yaml
version: '3.8'

services:
  ollama:
    image: ollama/ollama:latest
    ports:
      - "127.0.0.1:11434:11434"  # Localhost only
    volumes:
      - ollama_data:/root/.ollama
    environment:
      - OLLAMA_NUM_PARALLEL=4
      - OLLAMA_MAX_LOADED_MODELS=2
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
    restart: always
    logging:
      driver: "json-file"
      options:
        max-size: "100m"
        max-file: "5"
```

---

## 12. Troubleshooting

### GPU Not Detected

#### Symptom
Container logs show no GPU detection or inference runs on CPU.

#### Solutions

1. **Verify Docker GPU access**:
   ```bash
   docker run --rm --gpus all nvidia/cuda:12.4.0-base-ubuntu22.04 nvidia-smi
   ```

2. **Check NVIDIA Container Toolkit**:
   ```bash
   nvidia-ctk --version
   nvidia-ctk runtime configure --runtime=docker
   sudo systemctl restart docker
   ```

3. **Verify Docker daemon config** (`/etc/docker/daemon.json`):
   ```json
   {
     "runtimes": {
       "nvidia": {
         "path": "nvidia-container-runtime",
         "runtimeArgs": []
       }
     }
   }
   ```

### GPU Detected but Not Used

#### Symptom
GPU shows in logs but inference is slow (CPU-like speed).

#### Solutions

1. **Check container logs**:
   ```bash
   docker logs ollama | grep -i cuda
   docker logs ollama | grep -i gpu
   ```

2. **Enable debug logging**:
   ```bash
   docker run -e OLLAMA_DEBUG=2 ...
   ```

3. **Downgrade Ollama** (if recent update broke GPU):
   ```bash
   docker run ... ollama/ollama:0.3.13
   ```

### Permission Errors (AMD)

#### Symptom
"Permission denied" accessing `/dev/kfd` or `/dev/dri`.

#### Solutions

1. **Add render group**:
   ```yaml
   services:
     ollama:
       group_add:
         - render
   ```

2. **Use privileged mode** (less secure):
   ```yaml
   services:
     ollama:
       privileged: true
   ```

### Container Startup Failures

#### Symptom
Container exits immediately or restarts continuously.

#### Solutions

1. **Check logs**:
   ```bash
   docker logs ollama
   ```

2. **Run interactively**:
   ```bash
   docker run -it --gpus all ollama/ollama /bin/bash
   ```

3. **Check resource availability**:
   ```bash
   nvidia-smi
   free -h
   ```

### Driver Mismatch

#### Symptom
CUDA errors mentioning version incompatibility.

#### Solutions

1. **Check driver version**:
   ```bash
   nvidia-smi
   ```

2. **Use matching CUDA image**:
   - Driver 525+ = CUDA 12.x
   - Driver 450+ = CUDA 11.x

### Docker Compose GPU Error

#### Symptom
"Additional properties are not allowed ('devices' was unexpected)"

#### Solution
Upgrade Docker Compose to v2.x+:
```bash
docker compose version  # Should be 2.x
```

### Model Loading Failures

#### Symptom
"out of memory" or model fails to load.

#### Solutions

1. **Check available VRAM**:
   ```bash
   nvidia-smi
   ```

2. **Use smaller model or quantization**:
   ```bash
   docker exec ollama ollama pull llama3.2:8b-q4_0
   ```

3. **Limit concurrent models**:
   ```yaml
   environment:
     - OLLAMA_MAX_LOADED_MODELS=1
   ```

---

## 13. Security Considerations

### Default Security Posture

Ollama has **no built-in authentication**. By default:
- API is open to anyone who can reach port 11434
- No rate limiting
- No access logging (without debug mode)

### Network Exposure

#### Never Expose Directly to Internet

Research shows thousands of unsecured Ollama instances exposed publicly.

#### Bind to Localhost Only

```yaml
services:
  ollama:
    ports:
      - "127.0.0.1:11434:11434"  # Localhost only
```

#### Use Docker Networks

```yaml
services:
  ollama:
    # No ports exposed to host
    networks:
      - internal

  your_app:
    networks:
      - internal

networks:
  internal:
    internal: true  # No external access
```

### Adding Authentication

#### Reverse Proxy with Basic Auth (Caddy)

```yaml
services:
  ollama:
    image: ollama/ollama:latest
    networks:
      - internal
    # No ports exposed

  caddy:
    image: caddy:alpine
    ports:
      - "11434:11434"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
    networks:
      - internal

networks:
  internal:
```

**Caddyfile**:
```
:11434 {
    basicauth {
        admin $2a$14$... # bcrypt hash
    }
    reverse_proxy ollama:11434
}
```

#### API Key Authentication

Use projects like [ollama-auth](https://github.com/g1ibby/ollama-auth) for API key-based authentication.

### Container Privileges

#### Avoid `--privileged`

Only use when absolutely necessary (AMD GPU workarounds). Instead, use specific device mappings.

#### Resource Limits

```yaml
services:
  ollama:
    deploy:
      resources:
        limits:
          cpus: '8'
          memory: 32G
```

### Security Checklist

- [ ] Bind to localhost or internal network only
- [ ] Use reverse proxy with authentication for remote access
- [ ] Set resource limits to prevent DoS
- [ ] Enable logging for audit trail
- [ ] Keep Ollama and models updated
- [ ] Use non-root user if possible (custom image)
- [ ] Implement rate limiting at proxy level
- [ ] Use HTTPS for remote connections

---

## Sources

### Official Documentation
- [Ollama Docker Documentation](https://docs.ollama.com/docker)
- [Ollama Docker Hub](https://hub.docker.com/r/ollama/ollama)
- [NVIDIA Container Toolkit Installation Guide](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html)
- [Docker Compose GPU Support](https://docs.docker.com/compose/how-tos/gpu-support/)

### Community Resources
- [How to run Ollama with Docker Compose and GPU support](https://sleeplessbeastie.eu/2025/12/04/how-to-run-ollama-with-docker-compose-and-gpu-support/)
- [Ollama with AMD GPU (ROCm)](https://dev.to/kokizzu/ollama-with-amd-gpu-rocm-1p3i)
- [Apple Silicon GPUs, Docker and Ollama: Pick two](https://chariotsolutions.com/blog/post/apple-silicon-gpus-docker-and-ollama-pick-two/)
- [Ollama GPU Acceleration Guide](https://collabnix.com/ollama-gpu-acceleration-the-ultimate-nvidia-cuda-and-amd-rocm-configuration-guide-for-production-ai-deployment/)
- [Understanding and Securing Exposed Ollama Instances](https://www.upguard.com/blog/understanding-and-securing-exposed-ollama-instances)
- [LLM Performance on Mac: Native vs Docker](https://www.vchalyi.com/blog/2025/ollama-performance-benchmark-macos/)

### GitHub Resources
- [NVIDIA Container Toolkit GitHub](https://github.com/NVIDIA/nvidia-container-toolkit)
- [Ollama GitHub Issues - Troubleshooting](https://github.com/ollama/ollama/issues)
- [Ollama FAQ](https://docs.ollama.com/faq)

---

## Quick Reference

### Essential Commands

```bash
# Start with NVIDIA GPU
docker run -d --gpus all -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama

# Start with AMD GPU
docker run -d --device /dev/kfd --device /dev/dri -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama:rocm

# Pull a model
docker exec ollama ollama pull llama3.2

# Check GPU detection
docker logs ollama | grep -i gpu

# Verify API
curl http://localhost:11434/api/tags
```

### Essential Environment Variables

```bash
OLLAMA_NUM_PARALLEL=4          # Parallel requests per model
OLLAMA_MAX_LOADED_MODELS=2     # Concurrent models
OLLAMA_KEEP_ALIVE=10m          # Model memory retention
OLLAMA_DEBUG=1                 # Debug logging
CUDA_VISIBLE_DEVICES=0,1       # NVIDIA GPU selection
ROCR_VISIBLE_DEVICES=0         # AMD GPU selection
```
