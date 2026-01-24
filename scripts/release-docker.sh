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
  "${TAGS[@]}" \
  --build-arg "VERSION=${VERSION}" \
  --build-arg "COMMIT_SHA=${GITHUB_SHA}" \
  --label "org.opencontainers.image.version=${VERSION}" \
  --label "org.opencontainers.image.revision=${GITHUB_SHA}" \
  --cache-from "type=gha" \
  --cache-to "type=gha,mode=max" \
  --file ./Dockerfile .

echo "Docker image ${IMAGE}:${VERSION} pushed successfully"
