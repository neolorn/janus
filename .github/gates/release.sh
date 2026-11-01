#!/usr/bin/env bash

# CONV-SETUP-003 AC2, LIB-TEST-002 AC1, LIB-VER-001, LIB-VER-002 and CONV-VCS-005 AC3:
# the shipped surface moves only in a release commit, which adds its version's section
# to the changelog, empties Unreleased and every unshipped file, and names a version
# raised by what the shipped surface lost or gained; a section opening a major version
# links its migration note; a release tag names the version its commit added.
# LIB-VER-001 and CONV-VCS-005 (D-232): a release candidate is X.Y.Z-rc.N, numbered
# from 1 for each version and tagged vX.Y.Z-rc.N. Before the first final release no
# version is judged against the contract; from it on, a version is judged against the
# newest final release, and a release candidate is held to what its version needs.
# REF-001 AC2 and LIB-API-001: the version is raised as well by what the contract's
# lists lost or gained since the previous release: the configuration keys, the
# library-owned schema, the error codes and the status each answers with, the HTTP
# endpoints with the browser profile's stage order, the audit actions, the
# permissions, and the commands of the command-line tool with their arguments,
# standard input and output and exit codes (D-206).
# CONV-VCS-003 AC2: a commit that breaks the contract as released carries the breaking
# marker; it breaks it where it removes or changes a line the newest final release
# shipped, marks one removed in an unshipped file, or takes away an entry of a list
# that release held. A release candidate is never that baseline, and before the first
# final release no commit breaks the contract (D-232). A
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
  tests/Janus.Hosting.Tests/error-statuses.txt
  tests/Janus.Hosting.Tests/endpoints.txt
  tests/Janus.Cli.Tests/commands.txt
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

# The version sections of the changelog at a commit, newest first, a release
# candidate's with its suffix.
versions() {
  git show "$1:CHANGELOG.md" 2>/dev/null \
    | sed -nE 's/^## \[([0-9]+\.[0-9]+\.[0-9]+(-rc\.[1-9][0-9]*)?)\] - [0-9]{4}-[0-9]{2}-[0-9]{2}$/\1/p' || true
}

# The lines a section holds, from its heading to the next heading of its level. The
# changelog is read to its end: stopping early would close the pipe on git.
section() {
  git show "$1:CHANGELOG.md" | awk -v heading="## [$2]" '
    !done && index($0, heading) == 1 { inside = 1; next }
    inside && /^## / { inside = 0; done = 1 }
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

# The lines of the shipped files at a commit, each named for its file, blank lines and
# the nullable marker aside.
published() {
  local file

  for file in $(git ls-tree -r --name-only "$1" | grep -E '(^|/)PublicAPI\.Shipped\.txt$' || true); do
    git show "$1:${file}" | grep -vE '^(#nullable enable)?[[:space:]]*$' | sed "s|^|${file} |" || true
  done | LC_ALL=C sort -u
}

# The shipped lines a commit took away, a line changed among them, and those it marks
# removed in an unshipped file, which is how the analyser records a shipped line taken
# away before the release that drops it; each is named for its shipped file.
withdrawn() {
  {
    git diff "$1" "$2" -- '*PublicAPI.Shipped.txt' | awk '
      /^--- / { file = substr($0, 7); next }
      /^\+\+\+ / { next }
      /^-/ { line = substr($0, 2); if (line !~ /^(#nullable enable)?[[:space:]]*$/) print file " " line }'
    git diff "$1" "$2" -- '*PublicAPI.Unshipped.txt' | awk '
      /^\+\+\+ / { file = substr($0, 7); sub(/PublicAPI\.Unshipped\.txt$/, "PublicAPI.Shipped.txt", file); next }
      /^\+\*REMOVED\*/ { print file " " substr($0, 11) }'
  } | LC_ALL=C sort -u
}

# The contract's lists at a commit, one entry a line and each named for its list: the
# configuration keys, the library-owned schema, the codes' statuses, the endpoints and
# the commands as their contract tests hold them, and the codes and names as their catalogues
# declare them, read from the literal each declaration parses. A schema line is named
# for its relation, since a column's line alone reads the same in every table that has
# it. An endpoint's line is named for its endpoint for the same reason, and under it for
# the request or the status it belongs to; each code a status carries is an entry of
# its own, so a code gained is not read as a status lost; a stage of the browser
# profile is named with its place, so stages reordered are stages lost. A command's line
# is named for its command, and under it for its arguments, its input or its exit code;
# each code a refusal carries is an entry of its own; a line it writes on success is
# named with its place, so lines reordered are lines lost, and a member under it is
# named for that line, since two lines can hold a member of one name.
contract() {
  {
    git show "$1:tests/Janus.Core.Tests/configuration-keys.txt" 2>/dev/null \
      | sed -n 's/^\(..*\)$/configuration key \1/p' || true
    git show "$1:tests/Janus.Storage.Tests/schema.txt" 2>/dev/null \
      | awk '/^[^ ]/ { relation = $2; print "schema " $0; next } NF { print "schema " relation ":" $0 }' || true
    git show "$1:tests/Janus.Hosting.Tests/error-statuses.txt" 2>/dev/null \
      | sed -n 's/^\(..*\)$/error status \1/p' || true
    git show "$1:tests/Janus.Hosting.Tests/endpoints.txt" 2>/dev/null \
      | awk '
        /^[^ ]/ { endpoint = $0; place = 0; print "endpoint " $0; next }
        { line = $0; sub(/^ +/, "", line) }
        /^  [^ ]/ && endpoint == "pipeline" && $1 !~ /^[0-9]+$/ { print "endpoint pipeline:" ++place " " line; next }
        /^  [^ ]/ && $1 == "request" { part = $1; print "endpoint " endpoint ":" line; next }
        /^  [^ ]/ {
          part = $1
          print "endpoint " endpoint ":" part
          for (code = 2; code <= NF; code++) print "endpoint " endpoint ":" part " " $code
          next
        }
        NF { print "endpoint " endpoint ":" part ":" line }' || true
    git show "$1:tests/Janus.Cli.Tests/commands.txt" 2>/dev/null \
      | awk '
        /^[^ ]/ { command = $0; print "command " $0; next }
        { line = $0; sub(/^ +/, "", line) }
        /^  [^ ]/ && $1 == "input" { part = $1; print "command " command ":" line; next }
        /^  [^ ]/ {
          part = $1
          place = 0
          print "command " command ":" part
          for (code = 2; code <= NF; code++) print "command " command ":" part " " $code
          next
        }
        /^    [^ ]/ && part ~ /^[0-9]+$/ { output = ++place " " line; print "command " command ":" part ":" output; next }
        /^      [^ ]/ { print "command " command ":" part ":" output ":" line; next }
        NF { print "command " command ":" part ":" line }' || true

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

# Whether the first version comes before the second by SemVer 2.0.0 precedence: by
# their major, minor and patch numbers, and for one version, its release candidates in
# their order before the final release.
precedes() {
  local first second place

  IFS=. read -r -a first <<< "${1%%-rc.*}"
  IFS=. read -r -a second <<< "${2%%-rc.*}"

  for place in 0 1 2; do
    if [ "${first[$place]}" -lt "${second[$place]}" ]; then
      return 0
    elif [ "${first[$place]}" -gt "${second[$place]}" ]; then
      return 1
    fi
  done

  if [ "${1%%-rc.*}" = "$1" ]; then
    return 1
  elif [ "${2%%-rc.*}" = "$2" ]; then
    return 0
  fi

  [ "${1##*-rc.}" -lt "${2##*-rc.}" ]
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
  final=$(printf '%s\n' "$before" | grep -vF -- '-rc.' | head -n 1 || true)
  lost=$(shipped "$from" "$commit" -)
  gained=$(shipped "$from" "$commit" +)

  breaking=""

  if [ -n "$final" ]; then
    holder=$(released "$parent" "$final")

    if [ -z "$holder" ]; then
      echo "${commit}: the commit that released ${final} is not in the history, so what the commit takes away cannot be judged."
      status=1
    else
      gone=$(LC_ALL=C comm -12 <(withdrawn "$from" "$commit") <(published "$holder"))

      if [ -n "$gone" ]; then
        breaking="it removes, changes or marks removed what ${final} shipped: $(printf '%s\n' "$gone" | paste -sd ';' - | sed 's/;/; /g')"
      elif ! git diff --quiet "$parent" "$commit" -- "${listed[@]}"; then
        if legible "$holder" && legible "$parent" && legible "$commit"; then
          taken=$(LC_ALL=C comm -12 \
            <(LC_ALL=C comm -23 <(contract "$parent") <(contract "$commit")) \
            <(contract "$holder"))

          if [ -n "$taken" ]; then
            breaking="it takes away what ${final} released: $(printf '%s\n' "$taken" | paste -sd ';' - | sed 's/;/; /g')"
          fi
        else
          status=1
        fi
      fi
    fi
  fi

  if [ -n "$breaking" ] && ! marked "$commit"; then
    echo "${commit}: the commit breaks the contract, since ${breaking}, and carries no breaking marker."
    status=1
  fi

  added=$(comm -13 <(printf '%s\n' "$before" | sed '/^$/d' | sort) <(printf '%s\n' "$after" | sed '/^$/d' | sort))
  count=$(printf '%s' "$added" | grep -c . || true)

  for tag in $(git tag --points-at "$commit" | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+(-rc\.[0-9]+)?$' || true); do
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

  unreleased=$(section "$commit" "Unreleased")

  if grep -q '^- ' <<<"$unreleased"; then
    echo "${commit}: the release leaves lines under Unreleased."
    status=1
  fi

  for file in $(git ls-tree -r --name-only "$commit" | grep -E '(^|/)PublicAPI\.Unshipped\.txt$'); do
    shipped=$(git show "${commit}:${file}")

    if [ -n "$(grep -vE '^(#nullable enable)?[[:space:]]*$' <<<"$shipped" || true)" ]; then
      echo "${commit}: the release leaves lines in ${file}."
      status=1
    fi
  done

  # LIB-VER-001 and D-232: a release follows the newest release by SemVer precedence,
  # a release candidate numbered from 1 for its version. Before the first final
  # release nothing binds the version to the contract; from it on, the version is
  # judged against the newest final release, never a release candidate, and
  # X.Y.Z-rc.N is held to what X.Y.Z needs.
  previous=$latest
  baseline=$final
  target=${added%%-rc.*}
  part=major

  if [ -n "$previous" ] && ! precedes "$previous" "$added"; then
    echo "${commit}: ${added} does not follow ${previous}."
    status=1
  fi

  if [ "$target" != "$added" ]; then
    expected=1

    if [ -n "$previous" ] && [ "${previous%%-rc.*}" = "$target" ] && [ "${previous%%-rc.*}" != "$previous" ]; then
      expected=$(( ${previous##*-rc.} + 1 ))
    fi

    if [ "${added##*-rc.}" != "$expected" ]; then
      echo "${commit}: ${added} is not the next release candidate of ${target}, which is rc.${expected}."
      status=1
    fi
  fi

  if [ -n "$baseline" ]; then
    part=$(raised "$baseline" "$target")
    prior=$(released "$parent" "$baseline")
    dropped=""
    grown=""
    shed=0
    grew=0

    if [ -z "$prior" ]; then
      echo "${commit}: the commit that released ${baseline} is not in the history, so what the contract lost since cannot be judged."
      status=1
    else
      shed=$(shipped "$prior" "$commit" -)
      grew=$(shipped "$prior" "$commit" +)

      if legible "$prior" && legible "$commit"; then
        dropped=$(LC_ALL=C comm -23 <(contract "$prior") <(contract "$commit"))
        grown=$(LC_ALL=C comm -13 <(contract "$prior") <(contract "$commit"))
      else
        status=1
      fi
    fi

    if [ -z "$part" ]; then
      echo "${commit}: ${target} does not follow ${baseline}, the newest final release."
      status=1
    else
      if [ "$shed" -gt 0 ] && [ "$part" != major ]; then
        echo "${commit}: since ${baseline} the shipped surface lost ${shed} lines and ${added} raises the ${part} version."
        status=1
      elif [ "$grew" -gt 0 ] && [ "$part" = patch ]; then
        echo "${commit}: since ${baseline} the shipped surface gained ${grew} lines and ${added} raises the patch version."
        status=1
      fi

      if [ -n "$dropped" ] && [ "$part" != major ]; then
        echo "${commit}: since ${baseline} the contract's lists lost $(printf '%s\n' "$dropped" | grep -c .) entries and ${added} raises the ${part} version."
        printf '%s\n' "$dropped" | sed 's/^/  lost: /'
        status=1
      elif [ -n "$grown" ] && [ "$part" = patch ]; then
        echo "${commit}: since ${baseline} the contract's lists gained $(printf '%s\n' "$grown" | grep -c .) entries and ${added} raises the patch version."
        printf '%s\n' "$grown" | sed 's/^/  gained: /'
        status=1
      fi
    fi
  fi

  # CONV-VCS-005 AC3 (D-232): a release candidate's section opens no major version.
  if [ "$part" = major ] && [ "$target" = "$added" ]; then
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
