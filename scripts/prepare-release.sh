#!/usr/bin/env bash
#
# Promotes the "unshipped" surface records to their "shipped" baselines so a
# release reflects everything accumulated since the last one:
#
#   1. Each project's PublicAPI.Unshipped.txt -> PublicAPI.Shipped.txt
#   2. The generator's AnalyzerReleases.Unshipped.md -> AnalyzerReleases.Shipped.md
#      (under a new "## Release <version>" section)
#
# Run on a local branch named release/<semver>. It then builds the solution to
# validate the promoted files.
# This script does NOT commit, push, open a PR, or tag.
#
# Usage:
#   git switch -c release/1.0.0
#   scripts/prepare-release.sh

set -euo pipefail

# --- preflight ---

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

if [[ -n "$(git status --porcelain)" ]]; then
  echo "error: working tree is dirty. Commit or stash first." >&2
  exit 1
fi

CURRENT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"
if [[ ! "$CURRENT_BRANCH" =~ ^release/[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
  echo "error: must be on a 'release/<semver>' branch (currently on '$CURRENT_BRANCH')." >&2
  exit 1
fi
VERSION="${CURRENT_BRANCH#release/}"
TAG="v$VERSION"

echo "Fetching origin..."
git fetch --quiet origin main

# The release branch must fully contain current origin/main.
MERGE_BASE="$(git merge-base HEAD origin/main)"
if [[ "$MERGE_BASE" != "$(git rev-parse origin/main)" ]]; then
  echo "error: '$CURRENT_BRANCH' does not contain current origin/main." >&2
  echo "       Rebase or merge origin/main into the release branch first." >&2
  exit 1
fi

if git rev-parse --verify --quiet "refs/tags/$TAG" >/dev/null \
   || git ls-remote --tags --exit-code origin "refs/tags/$TAG" >/dev/null 2>&1; then
  echo "error: tag '$TAG' already exists." >&2
  exit 1
fi

echo "Preparing release $VERSION on '$CURRENT_BRANCH'"
echo

# --- 1. PublicAPI.Unshipped.txt -> PublicAPI.Shipped.txt ---

# Ordinal collation throughout so the sort matches StringComparer.Ordinal
export LC_ALL=C

promote_public_api() {
  local unshipped="$1"
  local shipped="${unshipped%Unshipped.txt}Shipped.txt"
  local tmp; tmp="$(mktemp -d)"

  # Additions: real entries (skip header, blanks, and *REMOVED* markers).
  grep -vE '^(#nullable enable)?$' "$unshipped" | grep -v '^\*REMOVED\*' \
    | sort -u > "$tmp/add.txt" || true

  # Removal targets: text after the *REMOVED* prefix.
  grep '^\*REMOVED\*' "$unshipped" | sed 's/^\*REMOVED\*//' \
    | sort -u > "$tmp/remove.txt" || true

  # Existing shipped entries.
  grep -vE '^(#nullable enable)?$' "$shipped" | sort -u > "$tmp/cur.txt" || true

  # (shipped ∪ additions) − removals
  sort -u "$tmp/cur.txt" "$tmp/add.txt" > "$tmp/union.txt"
  if [[ -s "$tmp/remove.txt" ]]; then
    comm -23 "$tmp/union.txt" "$tmp/remove.txt" > "$tmp/final.txt"
  else
    cp "$tmp/union.txt" "$tmp/final.txt"
  fi

  local added removed
  added="$(wc -l < "$tmp/add.txt" | tr -d ' ')"
  removed="$(wc -l < "$tmp/remove.txt" | tr -d ' ')"

  { echo "#nullable enable"; cat "$tmp/final.txt"; } > "$shipped"
  echo "#nullable enable" > "$unshipped"

  rm -rf "$tmp"
  echo "  promoted ${shipped#"$REPO_ROOT"/} (+${added} / -${removed})"
}

echo "PublicAPI:"
api_count=0
while IFS= read -r unshipped; do
  promote_public_api "$unshipped"
  api_count=$((api_count + 1))
done < <(find src -name 'PublicAPI.Unshipped.txt' | sort)
[[ "$api_count" -eq 0 ]] && echo "  (no PublicAPI files found)"
echo

# --- 2. AnalyzerReleases.Unshipped.md -> AnalyzerReleases.Shipped.md ---

ANALYZER_UNSHIPPED="src/Enver.Binding.Generator/AnalyzerReleases.Unshipped.md"
ANALYZER_SHIPPED="src/Enver.Binding.Generator/AnalyzerReleases.Shipped.md"

echo "AnalyzerReleases:"
if [[ -f "$ANALYZER_UNSHIPPED" ]] && grep -q '^### ' "$ANALYZER_UNSHIPPED"; then
  tmp="$(mktemp -d)"

  # Rule sections (from the first "### " to EOF), trimming trailing blank lines.
  awk '/^### /{f=1} f' "$ANALYZER_UNSHIPPED" \
    | awk '{ lines[NR]=$0 } END { last=NR; while (last>0 && lines[last]=="") last--; for (i=1;i<=last;i++) print lines[i] }' \
    > "$tmp/body.md"

  # Leading ";" comment headers of each file.
  grep '^;' "$ANALYZER_SHIPPED" > "$tmp/shipped_header.md" || true
  grep '^;' "$ANALYZER_UNSHIPPED" > "$tmp/unshipped_header.md" || true

  # Any previously-shipped release sections (from the first "## " to EOF).
  awk '/^## /{f=1} f' "$ANALYZER_SHIPPED" > "$tmp/prior.md" || true

  {
    cat "$tmp/shipped_header.md"
    echo
    echo "## Release $VERSION"
    echo
    cat "$tmp/body.md"
    if [[ -s "$tmp/prior.md" ]]; then
      echo
      cat "$tmp/prior.md"
    fi
  } > "$ANALYZER_SHIPPED"

  # Reset unshipped to just its comment header.
  cat "$tmp/unshipped_header.md" > "$ANALYZER_UNSHIPPED"

  rm -rf "$tmp"
  echo "  promoted $(grep -c '^ENVR' "$ANALYZER_SHIPPED" || echo 0) rule(s) into '## Release $VERSION'"
else
  echo "  (nothing unshipped to promote)"
fi
echo

# --- 3. validate ---

echo "Validating (dotnet build)..."
if dotnet build Enver.slnx --nologo -v quiet; then
  echo
  echo "Release $VERSION prepared on '$CURRENT_BRANCH'."
else
  echo
  echo "error: validation build failed. The promoted files were written but do not validate." >&2
  exit 1
fi
