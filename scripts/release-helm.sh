#!/usr/bin/env bash
set -euo pipefail
VERSION="$1"

REGISTRY="ghcr.io"
OCI_REPO="oci://${REGISTRY}/${GITHUB_REPOSITORY_OWNER}/csharp-compound-engineering/charts"

echo "Packaging Helm chart v${VERSION}..."

helm package charts/compound-docs \
  --version "${VERSION}" \
  --app-version "${VERSION}"

echo "Pushing Helm chart to ${OCI_REPO}..."

helm push "compound-docs-${VERSION}.tgz" "${OCI_REPO}"

echo "Helm chart v${VERSION} pushed successfully"
