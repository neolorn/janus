#!/usr/bin/env bash

# CONV-VCS-003 AC1: the form of a commit message is a gate, not a habit. A description
# over 72 characters, a body line that is prose rather than a dash fragment, and a
# type outside the list all fail here.

set -euo pipefail

base=${1:-}
head=${2:-HEAD}
empty=0000000000000000000000000000000000000000

if [ -z "$base" ] || [ "$base" = "$empty" ]; then
  range="${head} -1"
else
  range="${base}..${head}"
fi

# shellcheck disable=SC2086 # the range is two arguments when only the head is checked
commits=$(git rev-list ${range})
status=0

for commit in $commits; do
  subject=$(git log -1 --format=%s "$commit")
  body=$(git log -1 --format=%b "$commit")

  if ! printf '%s' "$subject" | grep -qE '^(feat|fix|refactor|perf|test|docs|build|ci|chore)(\([a-z0-9.-]+\))?!?: [^A-Z].*$'; then
    echo "${commit}: the description does not parse under Conventional Commits 1.0.0."
    echo "  ${subject}"
    status=1
  fi

  if [ "${#subject}" -gt 72 ]; then
    echo "${commit}: the description is ${#subject} characters, over 72."
    status=1
  fi

  while IFS= read -r line; do
    [ -z "$line" ] && continue

    if printf '%s' "$line" | grep -qE '^(BREAKING CHANGE|[A-Za-z][A-Za-z-]*): '; then
      continue
    fi

    if ! printf '%s' "$line" | grep -qE '^- '; then
      echo "${commit}: a body line is neither a dash fragment nor a footer trailer."
      echo "  ${line}"
      status=1
      continue
    fi

    if [ "${#line}" -gt 72 ]; then
      echo "${commit}: a body line is ${#line} characters, over 72."
      echo "  ${line}"
      status=1
    fi
  done <<< "$body"
done

if [ "$status" -eq 0 ]; then
  echo "Every commit message is in form."
fi

exit "$status"
