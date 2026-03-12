#!/usr/bin/env bash
set -euo pipefail

echo "=== Verifying release conditions ==="

# 1. C# build + test + 100% coverage enforcement
dotnet restore csharp-compounding-docs.sln
bash scripts/coverage-merge.sh

# 2. Docker build validation (single-platform, no push)
echo "=== Validating Docker builds ==="
docker buildx build --platform linux/amd64 --file ./Dockerfile .
docker buildx build --platform linux/amd64 --file ./Dockerfile.gitsync .

# 3. Lambda publish validation
echo "=== Validating Lambda publish ==="
dotnet publish src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj \
  --configuration Release --output /tmp/lambda-verify -p:UseAppHost=false
(cd /tmp/lambda-verify && zip -r /tmp/lambda-verify.zip .)
echo "Lambda publish validation passed ($(du -h /tmp/lambda-verify.zip | cut -f1))"
rm -rf /tmp/lambda-verify /tmp/lambda-verify.zip

# 4. OpenTofu syntax validation
echo "=== Validating OpenTofu configurations ==="
pwsh opentofu/serverless/scripts/deploy.ps1 validate
pwsh opentofu/k8s/scripts/deploy.ps1 validate

# 5. OpenTofu modules
for mod_dir in opentofu/modules/*/; do
  if [ -d "$mod_dir" ]; then
    echo "Validating ${mod_dir}..."
    tofu -chdir="$mod_dir" init -backend=false
    tofu -chdir="$mod_dir" validate
  fi
done

echo "=== All verifications passed ==="
