#!/usr/bin/env bash

# CONV-TEST-007 AC2: a test named for an acceptance criterion names an item that
# exists and a criterion that item has. A renamed or retired item leaves a test
# pointing at nothing, and the gate says so.

set -euo pipefail

status=0

names=$(grep -rhoE '\b[A-Z][A-Z0-9]*_[A-Z][A-Z0-9]*_[0-9]{3}[a-z]?_AC[0-9]+' --include='*.cs' tests | sort -u)

if [ -z "$names" ]; then
  echo "No acceptance-criterion test names found."
  exit 1
fi

for name in $names; do
  item=$(echo "${name%_AC*}" | tr '_' '-')
  criterion=${name##*_AC}

  # The declaring chapter is the one that opens a line with the item, whether the
  # bold marker is followed by a space or by a colon. A chapter that quotes an item
  # elsewhere, as `00` does to show the requirement format, states none of its
  # criteria.
  chapter=$(grep -rlE "^\*\*${item}\*\*[ :]" docs/spec || true)

  if [ -z "$chapter" ]; then
    echo "${name}: no item ${item} in docs/spec."
    status=1
    continue
  fi

  criteria=$(awk -v item="**${item}**" '
    index($0, item) == 1 && substr($0, length(item) + 1, 1) ~ /[ :]/ { inside = 1 }
    inside && /^---$/ { inside = 0 }
    inside && /^[0-9]+\. / { count = $1 + 0 }
    END { print count + 0 }
  ' "$(echo "$chapter" | head -1)")

  if [ "$criterion" -gt "$criteria" ]; then
    echo "${name}: ${item} has ${criteria} acceptance criteria, not ${criterion}."
    status=1
  fi
done

if [ "$status" -eq 0 ]; then
  echo "Every acceptance-criterion test name resolves to an item and a criterion."
fi

exit "$status"
