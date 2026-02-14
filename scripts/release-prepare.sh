#!/usr/bin/env bash
set -euo pipefail
VERSION="$1"

echo "Preparing release v${VERSION}..."

# Stamp Directory.Build.props
sed -i "s|<Version>[^<]*</Version>|<Version>${VERSION}</Version>|" Directory.Build.props
sed -i "s|<FileVersion>[^<]*</FileVersion>|<FileVersion>${VERSION}.0</FileVersion>|" Directory.Build.props
sed -i "s|<AssemblyVersion>[^<]*</AssemblyVersion>|<AssemblyVersion>${VERSION}.0</AssemblyVersion>|" Directory.Build.props

# Stamp Chart.yaml
sed -i "s|^version:.*|version: ${VERSION}|" charts/compound-docs/Chart.yaml
sed -i "s|^appVersion:.*|appVersion: \"${VERSION}\"|" charts/compound-docs/Chart.yaml

# Build first
dotnet restore csharp-compounding-docs.sln
bash scripts/coverage-merge.sh
dotnet build csharp-compounding-docs.sln \
  --configuration Release \
  -p:Version="${VERSION}"
echo "Release v${VERSION} prepared successfully"
