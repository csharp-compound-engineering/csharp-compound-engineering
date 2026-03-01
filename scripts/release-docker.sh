#!/usr/bin/env bash
set -euo pipefail
VERSION="$1"
CHANNEL="${2:-}"

REGISTRY="ghcr.io"
IMAGE="${REGISTRY}/${GITHUB_REPOSITORY}/mcp-server"
SHORT_SHA="${GITHUB_SHA:0:7}"
MAJOR=$(echo "$VERSION" | cut -d. -f1)
MINOR=$(echo "$VERSION" | cut -d. -f2)

TAGS=("--tag" "${IMAGE}:${VERSION}" "--tag" "${IMAGE}:sha-${SHORT_SHA}")

# Floating tags only for stable releases (no channel = stable)
if [ -z "$CHANNEL" ] || [ "$CHANNEL" = "null" ]; then
  TAGS+=("--tag" "${IMAGE}:latest" "--tag" "${IMAGE}:${MAJOR}" "--tag" "${IMAGE}:${MAJOR}.${MINOR}")
fi

echo "Building and pushing Docker image ${IMAGE}:${VERSION}..."

docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --push \
  --metadata-file /tmp/mcp-server-metadata.json \
  "${TAGS[@]}" \
  --build-arg "VERSION=${VERSION}" \
  --build-arg "COMMIT_SHA=${GITHUB_SHA}" \
  --label "org.opencontainers.image.version=${VERSION}" \
  --label "org.opencontainers.image.revision=${GITHUB_SHA}" \
  --cache-from "type=gha" \
  --cache-to "type=gha,mode=max" \
  --file ./Dockerfile .

echo "Docker image ${IMAGE}:${VERSION} pushed successfully"

# --- GitSync Job Image ---
GITSYNC_IMAGE="${REGISTRY}/${GITHUB_REPOSITORY}/gitsync-job"
GITSYNC_TAGS=("--tag" "${GITSYNC_IMAGE}:${VERSION}" "--tag" "${GITSYNC_IMAGE}:sha-${SHORT_SHA}")

if [ -z "$CHANNEL" ] || [ "$CHANNEL" = "null" ]; then
  GITSYNC_TAGS+=("--tag" "${GITSYNC_IMAGE}:latest" "--tag" "${GITSYNC_IMAGE}:${MAJOR}" "--tag" "${GITSYNC_IMAGE}:${MAJOR}.${MINOR}")
fi

echo "Building and pushing Docker image ${GITSYNC_IMAGE}:${VERSION}..."

docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --push \
  --metadata-file /tmp/gitsync-metadata.json \
  "${GITSYNC_TAGS[@]}" \
  --build-arg "VERSION=${VERSION}" \
  --build-arg "COMMIT_SHA=${GITHUB_SHA}" \
  --label "org.opencontainers.image.version=${VERSION}" \
  --label "org.opencontainers.image.revision=${GITHUB_SHA}" \
  --cache-from "type=gha" \
  --cache-to "type=gha,mode=max" \
  --file ./Dockerfile.gitsync .

echo "Docker image ${GITSYNC_IMAGE}:${VERSION} pushed successfully"

# --- Extract digests and update Helm chart + OpenTofu ---
MCP_DIGEST=$(jq -r '.["containerimage.digest"]' /tmp/mcp-server-metadata.json)
GITSYNC_DIGEST=$(jq -r '.["containerimage.digest"]' /tmp/gitsync-metadata.json)

echo "MCP server digest: ${MCP_DIGEST}"
echo "GitSync digest: ${GITSYNC_DIGEST}"

# Update values.yaml with digests
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

sed -i'' -e '/^image:/,/^[^ ]/ { /^  digest:/ s|digest:.*|digest: "'"${MCP_DIGEST}"'"|; }' "${REPO_ROOT}/charts/compound-docs/values.yaml"
sed -i'' -e '/^    image:/,/^[^ ]/ { /^      digest:/ s|digest:.*|digest: "'"${GITSYNC_DIGEST}"'"|; }' "${REPO_ROOT}/charts/compound-docs/values.yaml"

# Update OpenTofu serverless gitsync_image_digest default
sed -i'' -e '/variable "gitsync_image_digest"/,/^}/ { /default/ s|default.*|default     = "'"${GITSYNC_DIGEST}"'"|; }' "${REPO_ROOT}/opentofu/serverless/phases/03-compute/variables.tf"

echo "Updated values.yaml and OpenTofu variables with image digests"
