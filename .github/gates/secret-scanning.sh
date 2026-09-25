#!/usr/bin/env bash

# OPS-DEP-004: gitleaks, as its own release at the pinned version, checked against the
# pinned SHA-256 before anything in it runs, over every commit of every branch and tag.

set -euo pipefail

: "${GITLEAKS_VERSION:?the pinned gitleaks version is not set}"
: "${GITLEAKS_SHA256:?the SHA-256 of the pinned archive is not set}"

archive="gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz"
scanner=$(mktemp -d)
trap 'rm -rf "$scanner"' EXIT

curl --fail --silent --show-error --location --output "$scanner/$archive" \
  "https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/${archive}"

if ! echo "${GITLEAKS_SHA256}  $scanner/$archive" | sha256sum --check --strict --status; then
  echo "The archive's SHA-256 is not the pinned one; the scanner was not run."
  exit 1
fi

tar --extract --gzip --file "$scanner/$archive" --directory "$scanner" gitleaks

# With no log options the scanner reads git log --full-history --all, which is every
# commit reachable from any branch or tag of the checkout.
"$scanner/gitleaks" git --config .gitleaks.toml --redact --verbose --no-banner --exit-code 1 .
