# Multi-stage Dockerfile for CompoundDocs MCP Server
# Builds a minimal, secure container image

# Build arguments
ARG DOTNET_VERSION=9.0
ARG VERSION=0.0.0
ARG COMMIT_SHA=unknown

# ============================================================================
# Stage 1: Build
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build

WORKDIR /src

# Copy solution and project files first for better caching
COPY *.sln Directory.Build.props Directory.Packages.props ./
COPY src/CompoundDocs.Common/*.csproj src/CompoundDocs.Common/
COPY src/CompoundDocs.McpServer/*.csproj src/CompoundDocs.McpServer/
COPY src/CompoundDocs.Vector/*.csproj src/CompoundDocs.Vector/
COPY src/CompoundDocs.Graph/*.csproj src/CompoundDocs.Graph/
COPY src/CompoundDocs.Bedrock/*.csproj src/CompoundDocs.Bedrock/
COPY src/CompoundDocs.GraphRag/*.csproj src/CompoundDocs.GraphRag/

# Restore dependencies
RUN dotnet restore src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj

# Copy source code
COPY src/ src/

# Build the application
ARG VERSION
RUN dotnet build src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj \
    --configuration Release \
    --no-restore \
    -p:Version=${VERSION}

# Publish the application
RUN dotnet publish src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj \
    --configuration Release \
    --no-build \
    --output /app/publish \
    -p:UseAppHost=false

# ============================================================================
# Stage 2: Runtime
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS runtime

# Install necessary packages and create non-root user
RUN apk add --no-cache \
    ca-certificates \
    tzdata \
    && addgroup -g 1000 -S appgroup \
    && adduser -u 1000 -S appuser -G appgroup

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=UTC \
    COMPOUNDDOCS_LOG_LEVEL=Information

# Build metadata labels
ARG VERSION
ARG COMMIT_SHA
LABEL org.opencontainers.image.title="CompoundDocs MCP Server" \
    org.opencontainers.image.description="Model Context Protocol server for compound documentation" \
    org.opencontainers.image.version="${VERSION}" \
    org.opencontainers.image.revision="${COMMIT_SHA}" \
    org.opencontainers.image.source="https://github.com/compound-docs/csharp-compounding-docs" \
    org.opencontainers.image.licenses="MIT" \
    org.opencontainers.image.base.name="mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine"

EXPOSE 3000

# Entry point
ENTRYPOINT ["dotnet", "CompoundDocs.McpServer.dll"]
