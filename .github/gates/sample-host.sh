#!/usr/bin/env bash

# LIB-TEST-004 AC1, CONV-GATE-001 and CONV-SETUP-002 (D-204): the sample host is built
# from the packages of this commit and nothing else. They are packed into a folder feed,
# the sample's solution is restored against them at the version they were packed at,
# which its own package versions read as LibraryVersion, the command-line tool is
# installed at that version into the sample's tool manifest, and the sample is built and
# its tests run. The sample keeps no lockfile, so its restore is not locked.
# Its packages are restored into a folder of the run's own, so a package of an earlier
# commit packed under the same version is never taken from the machine's cache.

set -euo pipefail

work=${SAMPLE_HOST_WORK:-${RUNNER_TEMP:-tmp}/sample-host}
feed=$work/feed
samples=samples
manifest=$samples/.config/dotnet-tools.json

rm -rf "$work"
mkdir -p "$feed"
work=$(cd "$work" && pwd)
feed=$work/feed

# The tool's installation writes its entry into the sample's committed manifest, which
# is put back as committed once the run ends.
trap 'git checkout -- "$manifest"' EXIT

dotnet pack Janus.slnx --configuration Release --no-restore --output "$feed"

packed=$(find "$feed" -maxdepth 1 -name 'Janus.Hosting.*.nupkg' -printf '%f\n')
version=${packed#Janus.Hosting.}
version=${version%.nupkg}

echo "The packages of this commit are at ${version}."

export NUGET_PACKAGES=$work/packages

dotnet restore "$samples/HostSample.slnx" \
  -p:LibraryVersion="$version" \
  --source https://api.nuget.org/v3/index.json \
  --source "$feed"

(
  cd "$samples"
  dotnet tool install Janus.Cli --local --version "$version" --add-source "$feed"
  dotnet build HostSample.slnx --no-restore -p:LibraryVersion="$version"
  dotnet test --solution HostSample.slnx --no-build
)
