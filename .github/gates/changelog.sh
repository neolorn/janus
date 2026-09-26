#!/usr/bin/env bash

# CONV-VCS-005 AC1: a change to the library's code carries its line under Unreleased,
# written for a reader of the package.

set -euo pipefail

base=${1:-}
head=${2:-HEAD}
empty=0000000000000000000000000000000000000000

# A force-pushed branch leaves the event's previous commit unreachable, which is a
# fact about the push and not about the work. The range then starts where the branch
# left the default branch.
if [ -n "$base" ] && [ "$base" != "$empty" ] && ! git cat-file -e "${base}^{commit}" 2>/dev/null; then
  base=$(git merge-base origin/main "$head" 2>/dev/null || git merge-base main "$head" 2>/dev/null || true)
fi

if [ -z "$base" ] || [ "$base" = "$empty" ]; then
  base="${head}^"
fi

# Each output is read whole before it is searched: a search that stops at its first
# match would close the pipe on git and fail the pipeline under pipefail.
changed=$(git diff --name-only "$base" "$head")

if ! grep -qE '^src/.*\.cs$' <<<"$changed"; then
  echo "No change to the library's code; no changelog line is due."
  exit 0
fi

if ! grep -qF '## [Unreleased]' CHANGELOG.md; then
  echo "CHANGELOG.md has no Unreleased section."
  exit 1
fi

lines=$(git diff "$base" "$head" -- CHANGELOG.md)

if ! grep -qE '^\+[^+]' <<<"$lines"; then
  echo "The library's code changed and CHANGELOG.md gained no line."
  exit 1
fi

echo "The change carries its changelog line."
