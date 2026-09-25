#!/usr/bin/env bash

# CONV-SETUP-003 AC2, LIB-TEST-002 AC1, LIB-VER-001, LIB-VER-002 and CONV-VCS-005 AC3:
# the shipped surface moves only in a release commit, which adds its version's section
# to the changelog, empties Unreleased and every unshipped file, and names a version
# raised by what the shipped surface lost or gained; a section opening a major version
# links its migration note; a release tag names the version its commit added. A commit
# with two parents is the merge commit the platform writes, which carries no change of
# its own, so only single-parent commits are inspected.

set -euo pipefail

base=${1:-}
head=${2:-HEAD}
empty=0000000000000000000000000000000000000000
nothing=$(git hash-object -t tree /dev/null)

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

  added=$(comm -13 <(printf '%s\n' "$before" | sed '/^$/d' | sort) <(printf '%s\n' "$after" | sed '/^$/d' | sort))
  count=$(printf '%s' "$added" | grep -c . || true)

  for tag in $(git tag --points-at "$commit" | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' || true); do
    if [ "${tag#v}" != "$added" ]; then
      echo "${commit}: the tag ${tag} names no version this commit added to the changelog."
      status=1
    fi
  done

  if [ "$count" -eq 0 ]; then
    if [ "$(shipped "$from" "$commit" -)" -gt 0 ] || [ "$(shipped "$from" "$commit" +)" -gt 0 ]; then
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

  previous=$(printf '%s\n' "$before" | head -n 1)
  lost=$(shipped "$from" "$commit" -)
  gained=$(shipped "$from" "$commit" +)
  part=major

  if [ -n "$previous" ]; then
    part=$(raised "$previous" "$added")

    if [ -z "$part" ]; then
      echo "${commit}: ${added} does not follow ${previous}."
      status=1
    elif [ "$lost" -gt 0 ] && [ "$part" != major ]; then
      echo "${commit}: the shipped surface lost ${lost} lines and ${added} raises the ${part} version over ${previous}."
      status=1
    elif [ "$gained" -gt 0 ] && [ "$part" = patch ]; then
      echo "${commit}: the shipped surface gained ${gained} lines and ${added} raises the patch version over ${previous}."
      status=1
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
  echo "Every release in the range is well formed."
fi

exit "$status"
