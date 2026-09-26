#!/usr/bin/env bash

# CONV-DEP-002: alerting is on for the repository, and the pipeline refuses a build
# that resolves a package with a known vulnerability rather than waiting for someone
# to read the alert.

set -euo pipefail

report=$(dotnet list Janus.slnx package --vulnerable --include-transitive)
echo "$report"

if grep -qE '^ +> ' <<<"$report"; then
  echo "A referenced package has a known vulnerability."
  exit 1
fi

echo "No referenced package has a known vulnerability."
