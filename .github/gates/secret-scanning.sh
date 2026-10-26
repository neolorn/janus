#!/usr/bin/env bash

# OPS-DEP-004: gitleaks, as its own release at the pinned version, checked against the
# pinned SHA-256 before anything in it runs, over every commit of every branch and tag,
# with the committed .gitleaks.toml as the one allow-list.

set -euo pipefail

: "${GITLEAKS_VERSION:?the pinned gitleaks version is not set}"
: "${GITLEAKS_SHA256:?the SHA-256 of the pinned archive is not set}"

# gitleaks reads a .gitleaksignore at the scanned repository's root whatever its flags
# say, so a list of findings beside the configuration fails the step (D-210).
ignored=$(git ls-files -z | tr '\0' '\n' | grep -E '(^|/)\.gitleaksignore$' || true)
if [ -e .gitleaksignore ] || [ -L .gitleaksignore ] || [ -n "$ignored" ]; then
  echo "The checked-out tree holds a .gitleaksignore; the only allow-list is .gitleaks.toml."
  exit 1
fi

archive="gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz"
scanner=$(mktemp -d)
nothing=$(mktemp -d)
trap 'rm -rf "$scanner" "$nothing"' EXIT

curl --fail --silent --show-error --location --output "$scanner/$archive" \
  "https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/${archive}"

if ! echo "${GITLEAKS_SHA256}  $scanner/$archive" | sha256sum --check --strict --status; then
  echo "The archive's SHA-256 is not the pinned one; the scanner was not run."
  exit 1
fi

tar --extract --gzip --file "$scanner/$archive" --directory "$scanner" gitleaks

# With no log options the scanner reads git log --full-history --all, which is every
# commit reachable from any branch or tag of the checkout. The ignore path is a folder
# that holds no file, and no baseline is given, so no finding is silenced but by
# .gitleaks.toml, and an inline gitleaks:allow marker silences nothing.
"$scanner/gitleaks" git --config .gitleaks.toml --ignore-gitleaks-allow \
  --gitleaks-ignore-path "$nothing" --redact --verbose --no-banner --exit-code 1 .
