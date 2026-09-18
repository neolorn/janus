#!/usr/bin/env bash

# CONV-VCS-005 AC1: a change to the library's code carries its line under Unreleased,
# written for a reader of the package.

set -euo pipefail

base=${1:-}
head=${2:-HEAD}
empty=0000000000000000000000000000000000000000

if [ -z "$base" ] || [ "$base" = "$empty" ]; then
  base="${head}^"
fi

if ! git diff --name-only "$base" "$head" | grep -qE '^src/.*\.cs$'; then
  echo "No change to the library's code; no changelog line is due."
  exit 0
fi

if ! grep -qF '## [Unreleased]' CHANGELOG.md; then
  echo "CHANGELOG.md has no Unreleased section."
  exit 1
fi

if ! git diff "$base" "$head" -- CHANGELOG.md | grep -qE '^\+[^+]'; then
  echo "The library's code changed and CHANGELOG.md gained no line."
  exit 1
fi

echo "The change carries its changelog line."
