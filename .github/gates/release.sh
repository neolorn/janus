#!/usr/bin/env bash

# CONV-SETUP-003 AC2, LIB-TEST-002 AC1, LIB-VER-001, LIB-VER-002 and CONV-VCS-005 AC3:
# the shipped surface moves only in a release commit, which adds its version's section
# to the changelog, empties Unreleased and every unshipped file, and names a version
# raised by what the shipped surface lost or gained; a section opening a major version
# links its migration note; a release tag names the version its commit added.
# REF-001 AC2 and LIB-API-001: the version is raised as well by what the contract's
# lists lost or gained since the previous release: the configuration keys, the
# library-owned schema, the error codes, the audit actions and the permissions.
# CONV-VCS-003 AC2: a commit that breaks the contract as released carries the breaking
# marker; it breaks it where it removes or changes a shipped line, marks one removed in
# an unshipped file, or takes away an entry of a list the previous release held. A
# commit with two parents is the merge commit the platform writes, which carries no
# change of its own, so only single-parent commits are inspected.

set -euo pipefail

base=${1:-}
head=${2:-HEAD}
empty=0000000000000000000000000000000000000000
nothing=$(git hash-object -t tree /dev/null)

# The catalogues of codes and names, each with the type its entries are declared as
# and the name its entries are listed under.
catalogues='src/Janus.Core/ErrorCodes.cs ErrorCode error code
src/Janus.Core/AuditActions.cs AuditAction audit action
src/Janus.Core/Permissions.cs Permission permission'

# Every file a list of the contract is read from.
listed=(
  tests/Janus.Core.Tests/configuration-keys.txt
  tests/Janus.Storage.Tests/schema.txt
  src/Janus.Core/ErrorCodes.cs
  src/Janus.Core/AuditActions.cs
  src/Janus.Core/Permissions.cs
)

# A force-pushed branch leaves the event's previous commit unreachable, which is a
# fact about the push and not about the work. The range then starts where the branch
# left the default branch.
if [ -n "$base" ] && [ "$base" != "$empty" ] && ! git cat-file -e "${base}^{commit}" 2>/dev/null; then
  base=$(git merge-base origin/main "$head" 2>/dev/null || git merge-base main "$head" 2>/dev/null || true)
fi

if [ -z "$base" ] || [ "$base" = "$empty" ]; then
  range="--no-walk ${head}"
else
  range="${base}..${head}"
fi

# The version sections of the changelog at a commit, newest first.
versions() {
  git show "$1:CHANGELOG.md" 2>/dev/null \
    | sed -nE 's/^## \[([0-9]+\.[0-9]+\.[0-9]+)\] - [0-9]{4}-[0-9]{2}-[0-9]{2}$/\1/p' || true
}

# The lines a section holds, from its heading to the next heading of its level.
section() {
  git show "$1:CHANGELOG.md" | awk -v heading="## [$2]" '
    index($0, heading) == 1 { inside = 1; next }
    inside && /^## / { exit }
    inside { print }'
}

# How many lines of the shipped files a commit took away (-) or added (+), blank
# lines and the nullable marker aside.
shipped() {
  git diff "$1" "$2" -- '*PublicAPI.Shipped.txt' \
    | grep -E "^[$3][^$3]" \
    | grep -vE "^[$3](#nullable enable)?[[:space:]]*$" \
    | grep -c . || true
}

# How many shipped lines a commit marks removed in an unshipped file, which is how the
# analyser records a shipped line taken away before the release that drops it.
removed() {
  git diff "$1" "$2" -- '*PublicAPI.Unshipped.txt' | grep -cE '^\+\*REMOVED\*' || true
}

# The contract's lists at a commit, one entry a line and each named for its list: the
# configuration keys and the library-owned schema as their contract tests hold them,
# and the codes and names as their catalogues declare them, read from the literal each
# declaration parses. A schema line is named for its relation, since a column's line
# alone reads the same in every table that has it.
contract() {
  {
    git show "$1:tests/Janus.Core.Tests/configuration-keys.txt" 2>/dev/null \
      | sed -n 's/^\(..*\)$/configuration key \1/p' || true
    git show "$1:tests/Janus.Storage.Tests/schema.txt" 2>/dev/null \
      | awk '/^[^ ]/ { relation = $2; print "schema " $0; next } NF { print "schema " relation ":" $0 }' || true

    while read -r file type label; do
      git show "$1:${file}" 2>/dev/null \
        | grep -oE "\b${type}\.Parse\(\"[^\"]+\"\)" \
        | sed -E "s/^${type}\.Parse\(\"(.*)\"\)$/${label} \1/" || true
    done <<< "$catalogues"
  } | LC_ALL=C sort -u
}

# Whether the gate reads every entry of each catalogue at a commit. An entry declared
# any other way than with its literal would leave the list short and its loss unseen,
# so a catalogue read short is named rather than judged.
legible() {
  local file type label text declarations literals whole=0

  while read -r file type label; do
    text=$(git show "$1:${file}" 2>/dev/null || true)
    declarations=$(printf '%s\n' "$text" | grep -cE "public static ${type} [A-Za-z0-9_]+ \{ get; \}" || true)
    literals=$(printf '%s\n' "$text" | grep -oE "\b${type}\.Parse\(\"[^\"]+\"\)" | grep -c . || true)

    if [ "$declarations" -ne "$literals" ]; then
      echo "${1}: ${file} declares ${declarations} entries and the gate reads ${literals}; the ${label} list cannot be judged."
      whole=1
    fi
  done <<< "$catalogues"

  return "$whole"
}

# The commit that released a version: the newest whose changelog gained its section.
released() {
  local candidate

  for candidate in $(git log --format=%H -S"## [$2] - " "$1" -- CHANGELOG.md); do
    if versions "$candidate" | grep -qxF "$2" && ! versions "${candidate}^" | grep -qxF "$2"; then
      echo "$candidate"
      return
    fi
  done
}

# Whether a commit's message carries the breaking marker: the bang after the type or
# scope, or the footer.
marked() {
  git log -1 --format=%s "$1" | grep -qE '^[a-z]+(\([a-z0-9.-]+\))?!: ' \
    || git log -1 --format=%b "$1" | grep -qE '^BREAKING CHANGE: '
}

# Which part of the version a release raised over the one before it, each raised part
# resetting the parts below it; nothing where it does not follow.
raised() {
  IFS=. read -r was_major was_minor was_patch <<< "$1"
  IFS=. read -r major minor patch <<< "$2"

  if [ "$major" -gt "$was_major" ] && [ "$minor" -eq 0 ] && [ "$patch" -eq 0 ]; then
    echo major
  elif [ "$major" -eq "$was_major" ] && [ "$minor" -gt "$was_minor" ] && [ "$patch" -eq 0 ]; then
    echo minor
  elif [ "$major" -eq "$was_major" ] && [ "$minor" -eq "$was_minor" ] && [ "$patch" -gt "$was_patch" ]; then
    echo patch
  fi
}

# shellcheck disable=SC2086 # the range is two arguments when only the head is checked
commits=$(git rev-list --no-merges ${range})
status=0

for commit in $commits; do
  parent=$(git rev-parse -q --verify "${commit}^" || true)
  before=""
  [ -n "$parent" ] && before=$(versions "$parent")
  after=$(versions "$commit")
  from=${parent:-$nothing}
  latest=$(printf '%s\n' "$before" | head -n 1)
  lost=$(shipped "$from" "$commit" -)
  gained=$(shipped "$from" "$commit" +)

  breaking=""

  if [ "$lost" -gt 0 ]; then
    breaking="it removes or changes a shipped line"
  elif [ "$(removed "$from" "$commit")" -gt 0 ]; then
    breaking="it marks a shipped line removed"
  elif [ -n "$latest" ] && ! git diff --quiet "$parent" "$commit" -- "${listed[@]}"; then
    holder=$(released "$parent" "$latest")

    if [ -z "$holder" ]; then
      echo "${commit}: the commit that released ${latest} is not in the history, so what the commit takes away cannot be judged."
      status=1
    elif legible "$holder" && legible "$parent" && legible "$commit"; then
      taken=$(LC_ALL=C comm -12 \
        <(LC_ALL=C comm -23 <(contract "$parent") <(contract "$commit")) \
        <(contract "$holder"))

      if [ -n "$taken" ]; then
        breaking="it takes away what ${latest} released: $(printf '%s\n' "$taken" | paste -sd ';' - | sed 's/;/; /g')"
      fi
    else
      status=1
    fi
  fi

  if [ -n "$breaking" ] && ! marked "$commit"; then
    echo "${commit}: the commit breaks the contract, since ${breaking}, and carries no breaking marker."
    status=1
  fi

  added=$(comm -13 <(printf '%s\n' "$before" | sed '/^$/d' | sort) <(printf '%s\n' "$after" | sed '/^$/d' | sort))
  count=$(printf '%s' "$added" | grep -c . || true)

  for tag in $(git tag --points-at "$commit" | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' || true); do
    if [ "${tag#v}" != "$added" ]; then
      echo "${commit}: the tag ${tag} names no version this commit added to the changelog."
      status=1
    fi
  done

  if [ "$count" -eq 0 ]; then
    if [ "$lost" -gt 0 ] || [ "$gained" -gt 0 ]; then
      echo "${commit}: the shipped surface changed in a commit that releases no version."
      status=1
    fi

    continue
  fi

  if [ "$count" -gt 1 ]; then
    echo "${commit}: the commit adds ${count} version sections; a release adds one."
    status=1
    continue
  fi

  if [ "$added" != "$(printf '%s\n' "$after" | head -n 1)" ]; then
    echo "${commit}: the section for ${added} is not the newest in the changelog."
    status=1
  fi

  if section "$commit" "Unreleased" | grep -q '^- '; then
    echo "${commit}: the release leaves lines under Unreleased."
    status=1
  fi

  for file in $(git ls-tree -r --name-only "$commit" | grep -E '(^|/)PublicAPI\.Unshipped\.txt$'); do
    if git show "${commit}:${file}" | grep -vE '^(#nullable enable)?[[:space:]]*$' | grep -q .; then
      echo "${commit}: the release leaves lines in ${file}."
      status=1
    fi
  done

  previous=$latest
  part=major

  if [ -n "$previous" ]; then
    part=$(raised "$previous" "$added")
    prior=$(released "$parent" "$previous")
    dropped=""
    grown=""

    if [ -z "$prior" ]; then
      echo "${commit}: the commit that released ${previous} is not in the history, so what the contract lost since cannot be judged."
      status=1
    elif legible "$prior" && legible "$commit"; then
      dropped=$(LC_ALL=C comm -23 <(contract "$prior") <(contract "$commit"))
      grown=$(LC_ALL=C comm -13 <(contract "$prior") <(contract "$commit"))
    else
      status=1
    fi

    if [ -z "$part" ]; then
      echo "${commit}: ${added} does not follow ${previous}."
      status=1
    else
      if [ "$lost" -gt 0 ] && [ "$part" != major ]; then
        echo "${commit}: the shipped surface lost ${lost} lines and ${added} raises the ${part} version over ${previous}."
        status=1
      elif [ "$gained" -gt 0 ] && [ "$part" = patch ]; then
        echo "${commit}: the shipped surface gained ${gained} lines and ${added} raises the patch version over ${previous}."
        status=1
      fi

      if [ -n "$dropped" ] && [ "$part" != major ]; then
        echo "${commit}: since ${previous} the contract's lists lost $(printf '%s\n' "$dropped" | grep -c .) entries and ${added} raises the ${part} version."
        printf '%s\n' "$dropped" | sed 's/^/  lost: /'
        status=1
      elif [ -n "$grown" ] && [ "$part" = patch ]; then
        echo "${commit}: since ${previous} the contract's lists gained $(printf '%s\n' "$grown" | grep -c .) entries and ${added} raises the patch version."
        printf '%s\n' "$grown" | sed 's/^/  gained: /'
        status=1
      fi
    fi
  fi

  if [ "$part" = major ]; then
    linked=0

    for target in $(section "$commit" "$added" | grep -oE '\]\([^)#:]+\)' | sed -E 's/^\]\((.*)\)$/\1/' || true); do
      if git cat-file -e "${commit}:${target}" 2>/dev/null; then
        linked=1
      fi
    done

    if [ "$linked" -eq 0 ]; then
      echo "${commit}: the section for ${added} opens a major version and links no migration note in the repository."
      status=1
    fi
  fi
done

if [ "$status" -eq 0 ]; then
  echo "Every release in the range is well formed, and every break of the contract is marked."
fi

exit "$status"
