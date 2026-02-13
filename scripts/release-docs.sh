#!/usr/bin/env bash
set -euo pipefail
VERSION="$1"

echo "Building documentation for v${VERSION}..."

cd docs
pnpm install --frozen-lockfile
pnpm build
touch out/.nojekyll

echo "Documentation for v${VERSION} built successfully (output: docs/out/)"
