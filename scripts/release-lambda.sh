#!/usr/bin/env bash
set -euo pipefail
VERSION="$1"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

PUBLISH_DIR="/tmp/lambda-publish"
ZIP_FILE="${REPO_ROOT}/lambda-mcp-server-${VERSION}.zip"
VARS_FILE="${REPO_ROOT}/opentofu/serverless/phases/03-compute/variables.tf"
TFVARS_EXAMPLE="${REPO_ROOT}/opentofu/serverless/terraform.tfvars.example"

echo "Publishing McpServer for Lambda (v${VERSION})..."

rm -rf "${PUBLISH_DIR}"
dotnet publish "${REPO_ROOT}/src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj" \
  --configuration Release \
  --output "${PUBLISH_DIR}" \
  -p:UseAppHost=false \
  -p:Version="${VERSION}"

echo "Creating Lambda ZIP..."
(cd "${PUBLISH_DIR}" && zip -r "${ZIP_FILE}" .)
rm -rf "${PUBLISH_DIR}"

echo "Lambda ZIP: ${ZIP_FILE} ($(du -h "${ZIP_FILE}" | cut -f1))"

# Update variables.tf lambda_zip_version default
sed -i'' -e '/variable "lambda_zip_version"/,/^}/ { /default/ s|default.*|default     = "'"${VERSION}"'"|; }' "${VARS_FILE}"

# Update terraform.tfvars.example
sed -i'' -e 's|^# lambda_zip_version = .*|# lambda_zip_version = "'"${VERSION}"'"  # Set by CI|' "${TFVARS_EXAMPLE}"

echo "Lambda ZIP v${VERSION} packaged successfully"
