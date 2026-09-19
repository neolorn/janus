#!/usr/bin/env bash

# OPS-MIG-007: the migrations are applied twice against a throwaway database, once
# from empty and once from the previous release's schema. The second run is the one
# that catches a migration that works on a fresh database and breaks on a real one.
# OPS-DB-001: the locale belongs to the database, so every throwaway one is created
# under the ICU provider before a migration touches it.

set -euo pipefail

host=${PGHOST:-localhost}
port=${PGPORT:-5432}
user=${PGUSER:-postgres}
storage=src/Janus.Storage

create() {
  psql --quiet --no-psqlrc --host "$host" --port "$port" --username "$user" \
    --dbname postgres --command \
    "CREATE DATABASE $1 TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'und' LC_COLLATE 'C' LC_CTYPE 'C'"
}

apply() {
  dotnet ef database update --project "$1" --connection \
    "Host=${host};Port=${port};Username=${user};Password=${PGPASSWORD};Database=$2"
}

echo "Run one: from empty."
create janus_from_empty
apply "$storage" janus_from_empty

previous=$(git tag --list 'v*' --sort=-v:refname | head -n 1)

echo "Run two: from the previous release's schema."
create janus_from_previous

if [ -n "$previous" ]; then
  release=${RUNNER_TEMP:-/tmp}/janus-${previous}
  git worktree add --detach "$release" "$previous"
  apply "${release}/${storage}" janus_from_previous
  git worktree remove --force "$release"
else
  echo "No release is tagged yet, so the previous schema is the empty one."
fi

apply "$storage" janus_from_previous

echo "Both runs applied."
