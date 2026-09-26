#!/usr/bin/env bash

# OPS-DEP-002: the migrations a range adds are reported for their destructive
# operations whether or not the gate is enabled. OPS-DEP-001: the gate is the
# repository variable DESTRUCTIVE_DDL_GATE, `enabled` or `disabled`, so enabling it is
# a variable change and not a pipeline edit; enabled, a destructive operation fails the
# run and names the manual workflow, and an additive migration passes either way. The
# report is the idempotent SQL script of those migrations (`08` section 1b), which
# holds what each migration's Up runs and nothing of its Down, scanned for DROP,
# ALTER ... TYPE and an added constraint, and for the constraints OPS-DEP-001 names
# that could fail against rows a table already holds: SET NOT NULL, a unique index and
# a column added NOT NULL without a default, each on a table the same migrations did
# not create, or created and then filled.

set -euo pipefail

base=${1:-}
head=${2:-HEAD}
empty=0000000000000000000000000000000000000000
storage=src/Janus.Storage
workflow=.github/workflows/deploy-destructive.yml
gate=${DESTRUCTIVE_DDL_GATE:-disabled}

if [ "$gate" != enabled ] && [ "$gate" != disabled ]; then
  echo "DESTRUCTIVE_DDL_GATE is '${gate}'; it is enabled or disabled."
  exit 1
fi

# A force-pushed branch leaves the event's previous commit unreachable, which is a
# fact about the push and not about the work. The range then starts where the branch
# left the default branch.
if [ -n "$base" ] && [ "$base" != "$empty" ] && ! git cat-file -e "${base}^{commit}" 2>/dev/null; then
  base=$(git merge-base origin/main "$head" 2>/dev/null || git merge-base main "$head" 2>/dev/null || true)
fi

# The migrations a commit holds, in the order EF applies them, which is the order of
# their identifiers.
held() {
  git grep -h -oE '\[Migration\("[^"]+"\)\]' "$1" -- "${storage}/Migrations/*.cs" \
    | sed -E 's/^\[Migration\("(.*)"\)\]$/\1/' \
    | LC_ALL=C sort -u || true
}

# With no previous commit to compare with, no deployed migration is known, so every
# migration is one the deploy would apply.
before=""
if [ -n "$base" ] && [ "$base" != "$empty" ]; then
  before=$(held "$base")
fi

added=$(LC_ALL=C comm -13 <(printf '%s\n' "$before" | sed '/^$/d') <(held "$head" | sed '/^$/d'))

if [ -z "$added" ]; then
  echo "The range adds no migration, so no destructive operation."
  exit 0
fi

echo "Migrations the range adds:"
printf '%s\n' "$added" | sed 's/^/  /'

# The script is written from the migrations built here, taking each run of added
# migrations from the one before it, so a migration dated before one already applied
# is scripted as well.
dotnet build "$storage" --no-restore --nologo --verbosity quiet

scripts=$(mktemp -d)
trap 'rm -rf "$scripts"' EXIT

previous=0
first=""
last=""
runs=0

script() {
  runs=$((runs + 1))
  dotnet ef migrations script "$1" "$2" --idempotent --no-build \
    --project "$storage" --output "${scripts}/$(printf '%04d' "$runs").sql"
}

while IFS= read -r migration; do
  if printf '%s\n' "$added" | grep -qxF "$migration"; then
    [ -z "$first" ] && first=$previous
    last=$migration
  elif [ -n "$first" ]; then
    script "$first" "$last"
    first=""
  fi

  previous=$migration
done < <(held HEAD)

if [ -n "$first" ]; then
  script "$first" "$last"
fi

# Each statement EF writes sits in a block guarded by the migration's identifier. A
# block is cut at its semicolons, and each piece is judged on its own.
found=$(cat "${scripts}"/*.sql | sed 's/^\xEF\xBB\xBF//' | tr -d '\r' | awk '
  function named(text, after,    words, count) {
    if (!match(text, after)) {
      return ""
    }

    count = split(substr(text, RSTART, RLENGTH), words, " ")
    gsub(/"/, "", words[count])

    return words[count]
  }

  function report(piece) {
    sub(/^ +/, "", piece)

    if (length(piece) > 160) {
      piece = substr(piece, 1, 157) "..."
    }

    print migration ": " piece
  }

  function judge(piece,    upper, lower, table) {
    upper = toupper(piece)
    lower = tolower(piece)
    table = ""

    if (lower ~ /^ *create (unlogged |temp |temporary )?table /) {
      created[named(lower, "table (if not exists )?[^ (]+")] = 1
    }

    # A table the same migrations fill holds rows again, as one already deployed does.
    if (lower ~ /^ *insert into /) {
      delete created[named(lower, "^ *insert into [^ (]+")]
    }

    if (upper ~ /(^|[^A-Z0-9_])DROP([^A-Z0-9_]|$)/ \
        || upper ~ /(^|[^A-Z0-9_])ALTER[^A-Z0-9_](.*[^A-Z0-9_])?TYPE([^A-Z0-9_]|$)/ \
        || upper ~ /(^|[^A-Z0-9_])ADD +(CONSTRAINT|PRIMARY +KEY|UNIQUE|CHECK|FOREIGN +KEY|EXCLUDE)([^A-Z0-9_]|$)/) {
      report(piece)
      return
    }

    if (lower ~ /^ *alter table /) {
      table = named(lower, "^ *alter table (if exists )?(only )?[^ ]+")
    } else if (lower ~ /^ *create unique index /) {
      table = named(lower, " on (only )?[^ (]+")
    }

    if (table == "" || table in created) {
      return
    }

    if (lower ~ /(^|[^a-z0-9_])set +not +null([^a-z0-9_]|$)/ \
        || lower ~ /^ *create unique index / \
        || (lower ~ /(^|[^a-z0-9_])add([^a-z0-9_]|$)/ && lower ~ /not +null/ && lower !~ /(^|[^a-z0-9_])(default|generated)([^a-z0-9_]|$)/)) {
      report(piece)
    }
  }

  /"MigrationId" = / && / THEN$/ {
    split($0, quoted, "\047")
    migration = quoted[2]
    block = ""
    inside = 1
    next
  }

  inside && /^END \$EF\$;$/ {
    inside = 0
    sub(/[ \t]*END IF;[ \t]*$/, "", block)
    gsub(/[ \t]+/, " ", block)
    count = split(block, pieces, ";")

    for (at = 1; at <= count; at++) {
      if (pieces[at] ~ /[^ ]/) {
        judge(pieces[at])
      }
    }

    next
  }

  inside {
    block = block " " $0
  }
')

if [ -z "$found" ]; then
  echo "No destructive operation in the migrations the range adds."
  exit 0
fi

echo "Destructive operations:"
printf '%s\n' "$found" | sed 's/^/  /'

if [ "$gate" = enabled ]; then
  echo "The destructive-operation gate is enabled, so the automatic deploy stops here; run ${workflow} to apply these migrations."
  exit 1
fi

echo "The destructive-operation gate is disabled; reported only."
