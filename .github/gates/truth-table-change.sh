#!/usr/bin/env bash

# CONV-TEST-004 AC2 and CONV-VCS-004 AC1: a change to permission logic comes with a
# change to the truth table, whose diff is the change under review (AUTHZ-TEST-001).
# Permission logic is what the check and the filter decide by: the authorization area,
# the storage its ports are answered from, the declarations of the model builder, the
# mapping a host's filter reads the contract tables through, the view the filter
# reads, which only a migration writes, and the judgement of whether a session meets a
# step-up gate, which admits or refuses a bound action as the judgement from a host's
# report does: the session record, which keeps what was reached and the last downgrade
# (AUTH-SESS-009), and the gate's reading of it (AUTH-STEP-002 step 1), with the level
# the gate is read at (AUTH-STEP-002a, AUTH-STEP-006, AUTH-STEP-007). What a gate the
# session does not meet offers the account (AUTH-STEP-002 steps 2 and 3) admits and
# refuses nothing: it is held in a file of its own beside the judgement and is not
# watched (D-193), and neither are the services that resolve a gate's values from the
# policy, hold a step-up's challenge and answer its calls.

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
  'src/Janus.Authentication/Sessions/Session.cs'
  'src/Janus.Authentication/Factors/StepUp.cs'
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
