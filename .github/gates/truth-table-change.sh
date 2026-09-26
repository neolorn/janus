#!/usr/bin/env bash

# CONV-TEST-004 AC2 and CONV-VCS-004 AC1: a change to permission logic comes with a
# change to the truth table, whose diff is the change under review (AUTHZ-TEST-001).
# Permission logic is what the check and the filter decide by: the authorization area,
# the storage its ports are answered from, the declarations of the model builder, the
# mapping a host's filter reads the contract tables through, and the view the filter
# reads, which only a migration writes.

set -euo pipefail

base=${1:-}
head=${2:-HEAD}
empty=0000000000000000000000000000000000000000
table=tests/Janus.Hosting.Tests/Authorization/TruthTableTests.cs

logic=(
  'src/Janus.Authorization/*.cs'
  'src/Janus.Storage/Authorization/*.cs'
  'src/Janus.Core/AuthorizationDeclaration.cs'
  'src/Janus.Core/AuthorizationDeclarationBuilder.cs'
  'src/Janus.Core/DerivationDeclaration.cs'
  'src/Janus.Core/RelationshipDeclaration.cs'
  'src/Janus.Core/ResourceTypeDeclaration.cs'
  'src/Janus.Core/ResourceTypeDeclarationBuilder.cs'
  'src/Janus.Hosting/AuthorizationTables.cs'
)

# A force-pushed branch leaves the event's previous commit unreachable, which is a
# fact about the push and not about the work. The range then starts where the branch
# left the default branch.
if [ -n "$base" ] && [ "$base" != "$empty" ] && ! git cat-file -e "${base}^{commit}" 2>/dev/null; then
  base=$(git merge-base origin/main "$head" 2>/dev/null || git merge-base main "$head" 2>/dev/null || true)
fi

if [ -z "$base" ] || [ "$base" = "$empty" ]; then
  base="${head}^"
fi

changed=$(git diff --name-only "$base" "$head" -- "${logic[@]}")

# Each output is read whole before it is searched: a search that stops at its first
# match would close the pipe on git and fail the pipeline under pipefail.
migrations=$(git diff "$base" "$head" -- 'src/Janus.Storage/Migrations/*.cs')
rewritten=$(grep -E '^[-+][^-+]' <<<"$migrations" || true)

if grep -qw effective_grants <<<"$rewritten"; then
  changed=$(printf '%s\n%s' "$changed" "a migration changing the view effective_grants" | sed '/^$/d')
fi

if [ -z "$changed" ]; then
  echo "No change to permission logic; no truth-table change is due."
  exit 0
fi

if ! git diff --quiet "$base" "$head" -- "$table"; then
  echo "The permission-logic change carries its truth-table change."
  exit 0
fi

echo "Permission logic changed and ${table} did not:"
printf '%s\n' "$changed" | sed 's/^/  /'
exit 1
