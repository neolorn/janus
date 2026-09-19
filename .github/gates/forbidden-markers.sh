#!/usr/bin/env bash

# CONV-CODE-005 AC2: unfinished work is a task in the phase report, not a note in the
# source, and code that is no longer wanted is deleted rather than commented out.

set -euo pipefail

status=0

if markers=$(grep -rnE '\b(TODO|FIXME|HACK)\b' --include='*.cs' --include='*.csproj' --include='*.props' --include='*.sh' src tests tools); then
  echo "Forbidden marker:"
  echo "$markers"
  status=1
fi

# A comment that ends in a statement terminator or a brace is code someone commented
# out; prose does not end that way. Documentation comments are not comments of this
# kind and are left alone.
if commented=$(grep -rnE '^[[:space:]]*//[^/].*[;{}][[:space:]]*$' --include='*.cs' src tests tools); then
  echo "Commented-out code:"
  echo "$commented"
  status=1
fi

if [ "$status" -eq 0 ]; then
  echo "No forbidden marker and no commented-out code."
fi

exit "$status"
