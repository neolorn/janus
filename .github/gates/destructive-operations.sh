#!/usr/bin/env bash

# OPS-MIG-001, CONV-DESIGN-003 (D-206, D-225): the migrations a pull request or a push
# adds are reported for their destructive operations. The report states which
# statements are destructive and passes whatever it found, since whether one stops a
# deploy is the host's pipeline's. The range is passed base then head, and the
# migrations judged are those the head holds and the base does not.
#
# The report is the idempotent SQL script of those migrations (`08` section 1b), which
# holds what each migration's Up runs and nothing of its Down, scanned for DROP,
# ALTER ... TYPE, an added constraint, TRUNCATE and DELETE FROM, and, on a table the
# same migrations did not create or have filled, SET NOT NULL, a unique index and a
# column added NOT NULL without a default. Of those, the destructive ones are DROP TABLE,
# DROP COLUMN, DROP SCHEMA, any ALTER ... TYPE, TRUNCATE, DELETE FROM, and an added
# constraint, SET NOT NULL, a unique index or a column added NOT NULL without a default
# on a table the same migrations did not create or have filled. Every other match is
# listed as reported and not destructive.

set -euo pipefail

base=${1:-}
head=${2:-HEAD}

empty=0000000000000000000000000000000000000000
storage=src/Janus.Storage

# The migrations a commit holds, in the order EF applies them, which is the order of
# their identifiers.
held() {
  git grep -h -oE '\[Migration\("[^"]+"\)\]' "$1" -- "${storage}/Migrations/*.cs" \
    | sed -E 's/^\[Migration\("(.*)"\)\]$/\1/' \
    | LC_ALL=C sort -u || true
}

# A force-pushed branch leaves the event's previous commit unreachable, which is a fact
# about the push and not about the work. The range then starts where the branch left
# the default branch.
if [ -n "$base" ] && [ "$base" != "$empty" ] && ! git cat-file -e "${base}^{commit}" 2>/dev/null; then
  base=$(git merge-base origin/main "$head" 2>/dev/null || git merge-base main "$head" 2>/dev/null || true)
fi

# With no previous commit to compare with, no applied migration is known, so every
# migration is one the range adds.
before=""
if [ -n "$base" ] && [ "$base" != "$empty" ]; then
  before=$(held "$base")
fi

added=$(LC_ALL=C comm -13 <(printf '%s\n' "$before" | sed '/^$/d') <(held "$head" | sed '/^$/d'))

if [ -z "$added" ]; then
  echo "No migration is pending, so no destructive operation."
  exit 0
fi

echo "Migrations judged:"
printf '%s\n' "$added" | sed 's/^/  /'

# The script is written from the migrations built here, taking each run of pending
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
# block is cut at its semicolons, and each piece is judged on its own: a line starting
# "stop" is destructive, one starting "note" is reported and not destructive.
found=$(cat "${scripts}"/*.sql | sed 's/^\xEF\xBB\xBF//' | tr -d '\r' | awk '
  function named(text, after,    words, count) {
    if (!match(text, after)) {
      return ""
    }

    count = split(substr(text, RSTART, RLENGTH), words, " ")
    gsub(/"/, "", words[count])

    return words[count]
  }

  function report(kind, piece) {
    sub(/^ +/, "", piece)

    if (length(piece) > 160) {
      piece = substr(piece, 1, 157) "..."
    }

    print kind " " migration ": " piece
  }

  function judge(piece,    upper, lower, table, existing) {
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

    if (lower ~ /^ *alter table /) {
      table = named(lower, "^ *alter table (if exists )?(only )?[^ ]+")
    } else if (lower ~ /^ *create unique index /) {
      table = named(lower, " on (only )?[^ (]+")
    }

    existing = table != "" && !(table in created)

    # Data lost, whatever the table.
    if (upper ~ /(^|[^A-Z0-9_])DROP +(TABLE|COLUMN|SCHEMA)([^A-Z0-9_]|$)/ \
        || upper ~ /(^|[^A-Z0-9_])ALTER[^A-Z0-9_](.*[^A-Z0-9_])?TYPE([^A-Z0-9_]|$)/ \
        || upper ~ /(^|[^A-Z0-9_])TRUNCATE([^A-Z0-9_]|$)/ \
        || upper ~ /(^|[^A-Z0-9_])DELETE +FROM([^A-Z0-9_]|$)/) {
      report("stop", piece)
      return
    }

    # A constraint, which can fail only against rows a table already holds.
    if (upper ~ /(^|[^A-Z0-9_])ADD +(CONSTRAINT|PRIMARY +KEY|UNIQUE|CHECK|FOREIGN +KEY|EXCLUDE)([^A-Z0-9_]|$)/) {
      report(existing ? "stop" : "note", piece)
      return
    }

    if (existing \
        && (lower ~ /(^|[^a-z0-9_])set +not +null([^a-z0-9_]|$)/ \
            || lower ~ /^ *create unique index / \
            || (lower ~ /(^|[^a-z0-9_])add([^a-z0-9_]|$)/ && lower ~ /not +null/ && lower !~ /(^|[^a-z0-9_])(default|generated)([^a-z0-9_]|$)/))) {
      report("stop", piece)
      return
    }

    if (upper ~ /(^|[^A-Z0-9_])DROP([^A-Z0-9_]|$)/) {
      report("note", piece)
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

destructive=$(printf '%s\n' "$found" | sed -n 's/^stop //p')
reported=$(printf '%s\n' "$found" | sed -n 's/^note //p')

if [ -n "$destructive" ]; then
  echo "Destructive operations:"
  printf '%s\n' "$destructive" | sed 's/^/  /'
fi

if [ -n "$reported" ]; then
  echo "Reported and not destructive:"
  printf '%s\n' "$reported" | sed 's/^/  /'
fi

if [ -z "$destructive" ]; then
  echo "No destructive operation in the migrations judged."
fi
