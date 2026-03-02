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

# 3. OpenTofu syntax validation
echo "=== Validating OpenTofu configurations ==="
for phase_dir in opentofu/*/phases/*/; do
  if [ -d "$phase_dir" ]; then
    echo "Validating ${phase_dir}..."
    tofu -chdir="$phase_dir" init -backend=false
    tofu -chdir="$phase_dir" validate
  fi
done

# 4. OpenTofu modules
for mod_dir in opentofu/modules/*/; do
  if [ -d "$mod_dir" ]; then
    echo "Validating ${mod_dir}..."
    tofu -chdir="$mod_dir" init -backend=false
    tofu -chdir="$mod_dir" validate
  fi
done

echo "=== All verifications passed ==="
