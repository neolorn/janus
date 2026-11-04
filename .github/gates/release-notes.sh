#!/usr/bin/env bash

# CONV-VCS-005 and D-260: a release's notes are its version's section of the changelog,
# in the release's body where the section fits the platform's limit for a body, 125,000
# characters, and otherwise a short body linking CHANGELOG.md at the tag, with the
# section attached as release-notes-<version>.md beside the packages. The release
# workflow writes the notes with this script, and the release gate runs it on every
# release commit, so the form a release takes is known before its tag is pushed. It
# fails only where the changelog holds no section for the version.
#
# Usage: release-notes.sh <version> <changelog> <directory> <repository>
# Writes <directory>/body.md, and <directory>/release-notes-<version>.md where the
# section is attached, and says which form the notes take.

set -euo pipefail

version=$1
changelog=$2
directory=$3
repository=$4
limit=125000
heading="## [${version}] - "

if ! grep -qE "^## \[${version//./[.]}\] - [0-9]{4}-[0-9]{2}-[0-9]{2}$" "$changelog"; then
  echo "The changelog has no section for ${version}."
  exit 1
fi

mkdir -p "$directory"

# The section, from the line after its heading to the next heading of its level. The
# changelog is read to its end, so no pipe is closed early.
awk -v heading="$heading" '
  !done && index($0, heading) == 1 { inside = 1; next }
  inside && /^## / { inside = 0; done = 1 }
  inside { print }' "$changelog" > "$directory/section.md"

# The platform counts characters, so the section is measured in characters of UTF-8.
characters=$(LC_ALL=C.UTF-8 wc -m < "$directory/section.md" | tr -d '[:space:]')

if [ "$characters" -le "$limit" ]; then
  mv "$directory/section.md" "$directory/body.md"
  echo "The notes of ${version}, ${characters} characters, are the release's body."
  exit 0
fi

attached="release-notes-${version}.md"
mv "$directory/section.md" "$directory/$attached"
printf '%s\n' \
  "The notes of ${version} are longer than a release's body holds, so they are attached to this release as ${attached}." \
  "" \
  "They are the section of ${version} in [CHANGELOG.md](https://github.com/${repository}/blob/v${version}/CHANGELOG.md)." \
  > "$directory/body.md"
echo "The notes of ${version}, ${characters} characters, exceed the body's limit of ${limit} and are attached as ${attached}."
