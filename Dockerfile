# Multi-stage Dockerfile for CompoundDocs MCP Server
# Uses Ubuntu Chiseled images for minimal attack surface with non-root by default
#
# Version pins — update digests with:
#   docker buildx imagetools inspect <image>:<tag> --format '{{.Manifest.Digest}}'
#   SDK:     mcr.microsoft.com/dotnet/sdk:10.0-noble
#   Runtime: mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
ARG SDK_DIGEST=sha256:e362a8dbcd691522456da26a5198b8f3ca1d7641c95624fadc5e3e82678bd08a
ARG RUNTIME_DIGEST=sha256:b7dcab0a2c26dd114943605fa6aaa43f07956ed9ffcfcf63cc55cb9af5481779

# ============================================================================
# Stage 1: Build (runs on host arch, cross-compiles to TARGETARCH)
# ============================================================================
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk@${SDK_DIGEST} AS build

ARG TARGETARCH
ARG VERSION=0.0.0

WORKDIR /src

# Copy solution and project files first for better layer caching
COPY --link *.sln Directory.Build.props Directory.Packages.props ./
COPY --link src/CompoundDocs.Common/*.csproj src/CompoundDocs.Common/
COPY --link src/CompoundDocs.McpServer/*.csproj src/CompoundDocs.McpServer/
COPY --link src/CompoundDocs.Vector/*.csproj src/CompoundDocs.Vector/
COPY --link src/CompoundDocs.Graph/*.csproj src/CompoundDocs.Graph/
COPY --link src/CompoundDocs.Bedrock/*.csproj src/CompoundDocs.Bedrock/
COPY --link src/CompoundDocs.GraphRag/*.csproj src/CompoundDocs.GraphRag/
COPY --link src/CompoundDocs.GitSync/*.csproj src/CompoundDocs.GitSync/

# Restore dependencies (arch-specific)
RUN dotnet restore src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj \
    -a ${TARGETARCH}

# Copy source code
COPY --link src/ src/

# Publish in one step (build + publish combined)
RUN dotnet publish src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj \
    --configuration Release \
    --no-restore \
    -a ${TARGETARCH} \
    --output /app/publish \
    -p:UseAppHost=false \
    -p:Version=${VERSION} \
    -p:EnableSourceLink=false \
    -p:EnableSourceControlManagerQueries=false

# ============================================================================
# Stage 2: Runtime (Ubuntu Chiseled — non-root by default, UID 1654)
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet@${RUNTIME_DIGEST} AS runtime

WORKDIR /app

# Copy published application
COPY --link --from=build /app/publish .

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production \
    TZ=UTC \
    COMPOUNDDOCS_LOG_LEVEL=Information

# Build metadata labels
ARG VERSION=0.0.0
ARG COMMIT_SHA=unknown
LABEL org.opencontainers.image.title="CompoundDocs MCP Server" \
    org.opencontainers.image.description="Model Context Protocol server for compound documentation" \
    org.opencontainers.image.version="${VERSION}" \
    org.opencontainers.image.revision="${COMMIT_SHA}" \
    org.opencontainers.image.source="https://github.com/csharp-compound-engineering/csharp-compound-engineering" \
    org.opencontainers.image.licenses="MIT"

EXPOSE 8080

# Entry point
ENTRYPOINT ["dotnet", "CompoundDocs.McpServer.dll"]
