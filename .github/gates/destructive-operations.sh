#!/usr/bin/env bash

# OPS-DEP-002: the report runs whether or not the gate is enabled. OPS-DEP-001: the
# gate is the repository variable DESTRUCTIVE_GATE, so enabling it is a variable
# change and not a pipeline edit.

set -euo pipefail

migrations=src/Janus.Storage/Migrations
gate=${DESTRUCTIVE_GATE:-disabled}

if [ ! -d "$migrations" ]; then
  echo "No migrations, so no destructive operation."
  exit 0
fi

found=$(grep -rniE 'DROP (COLUMN|TABLE|CONSTRAINT)|ALTER COLUMN [^;]* TYPE |ADD CONSTRAINT' "$migrations" || true)

if [ -z "$found" ]; then
  echo "No destructive operation in the migrations."
  exit 0
fi

echo "Destructive operations:"
echo "$found"

if [ "$gate" = "enabled" ]; then
  echo "The destructive-operation gate is enabled; run the apply-destructive-migrations workflow."
  exit 1
fi

echo "The destructive-operation gate is disabled; reported only."
