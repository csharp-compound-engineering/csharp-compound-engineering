#!/usr/bin/env bash
set -euo pipefail

THRESHOLD=100
RESULTS_DIR="./TestResults"
OUTPUT_DIR="./CoverageResults"

# Clean previous results
rm -rf "$RESULTS_DIR" "$OUTPUT_DIR"

# ── Helper: extract attribute value from Cobertura XML ──
extract_rate() {
  local file="$1" attr="$2"
  sed -n "s/.*${attr}=\"\([^\"]*\)\".*/\1/p" "$file" | head -1
}

# ── Helper: check Cobertura coverage against threshold ──
check_coverage() {
  local report="$1"
  local label="$2"

  local line_rate branch_rate line_pct branch_pct failed=0
  line_rate=$(extract_rate "$report" "line-rate")
  branch_rate=$(extract_rate "$report" "branch-rate")
  line_pct=$(awk "BEGIN {printf \"%.2f\", $line_rate * 100}")
  branch_pct=$(awk "BEGIN {printf \"%.2f\", $branch_rate * 100}")

  echo "[$label] Line coverage:   ${line_pct}%"
  echo "[$label] Branch coverage: ${branch_pct}%"
  echo "[$label] Threshold:       ${THRESHOLD}%"

  if (( $(awk "BEGIN {print ($line_pct < $THRESHOLD)}") )); then
    echo "FAIL: [$label] Line coverage ${line_pct}% is below threshold ${THRESHOLD}%"
    failed=1
  fi
  if (( $(awk "BEGIN {print ($branch_pct < $THRESHOLD)}") )); then
    echo "FAIL: [$label] Branch coverage ${branch_pct}% is below threshold ${THRESHOLD}%"
    failed=1
  fi
  return $failed
}

COLLECT_ARGS=(--collect:"XPlat Code Coverage" --settings coverlet.runsettings)

# ── Step 1: Run each test project into a named subdirectory ──
dotnet test tests/CompoundDocs.Tests.Unit \
  "${COLLECT_ARGS[@]}" --results-directory "$RESULTS_DIR/Unit"

dotnet test tests/CompoundDocs.Tests.Integration \
  "${COLLECT_ARGS[@]}" --results-directory "$RESULTS_DIR/Integration"

dotnet test tests/CompoundDocs.Tests.E2E \
  "${COLLECT_ARGS[@]}" --results-directory "$RESULTS_DIR/E2E"

# ── Step 2: Ensure ReportGenerator is available ──
dotnet tool restore

# ── Step 3: Check Unit test coverage individually ──
echo ""
echo "=== Unit Test Coverage ==="
UNIT_REPORT=$(find "$RESULTS_DIR/Unit" -name "coverage.cobertura.xml" | head -1)
if [ -z "$UNIT_REPORT" ]; then
  echo "ERROR: Unit test coverage report not found"
  exit 1
fi
check_coverage "$UNIT_REPORT" "Unit Tests"

# ── Step 4: Merge all coverage into single report ──
echo ""
echo "=== Merged Coverage ==="
dotnet tool run reportgenerator \
  -reports:"$RESULTS_DIR/**/coverage.cobertura.xml" \
  -targetdir:"$OUTPUT_DIR" \
  -reporttypes:"Cobertura;Html"

MERGED_REPORT="$OUTPUT_DIR/Cobertura.xml"
echo "Merged coverage report: $MERGED_REPORT"
check_coverage "$MERGED_REPORT" "Merged (Unit + Integration + E2E)"

echo ""
echo "All coverage thresholds passed."

# ── Step 5: Create archive for CI artifact upload ──
tar -czf coverage-report.tar.gz -C "$OUTPUT_DIR" .
echo "Coverage archive: coverage-report.tar.gz"
