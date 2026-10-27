#!/usr/bin/env bash

# LIB-TEST-004 AC1, CONV-GATE-001 and CONV-SETUP-002 (D-204): the sample host is built
# from the packages of this commit and nothing else. They are packed into a folder feed,
# the sample's solution is restored against them at the version they were packed at,
# which its own package versions read as LibraryVersion, the command-line tool is
# installed at that version into the sample's tool manifest, and the sample is built and
# its tests run. The sample keeps no lockfile, so its restore is not locked.
# CONV-SETUP-004 (D-207): the format gate covers the sample's solution too. It reads the
# restored packages, so it runs here, once they are, with the version they were packed at
# given to its evaluation through the environment, as dotnet format takes no property.
# Its packages are restored into a folder of the run's own, so a package of an earlier
# commit packed under the same version is never taken from the machine's cache.
# LIB-TEST-004, CONV-LAYOUT-001: the restore and the tool's installation read a NuGet
# configuration written into the run's folder, never the repository, whose source
# mapping takes each of the five packages the library packs from the folder feed alone
# and every other package from nuget.org, so a package of the same identifier and
# version on nuget.org is never built in place of this commit's.

set -euo pipefail

work=${SAMPLE_HOST_WORK:-${RUNNER_TEMP:-tmp}/sample-host}
feed=$work/feed
samples=samples
manifest=$samples/.config/dotnet-tools.json

rm -rf "$work"
mkdir -p "$feed"
work=$(cd "$work" && pwd)
feed=$work/feed
sources=$work/nuget.config

# The feed is named relative to the configuration, as NuGet reads a local source.
cat > "$sources" <<'CONFIG'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="feed" value="feed" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="feed">
      <package pattern="Janus.Hosting" />
      <package pattern="Janus.Testing" />
      <package pattern="Janus.Cli" />
      <package pattern="Janus.Conformance" />
      <package pattern="Janus.Analyzers" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
CONFIG

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
  --configfile "$sources"

(
  cd "$samples"
  dotnet tool install Janus.Cli --local --version "$version" --configfile "$sources"
  dotnet build HostSample.slnx --no-restore -p:LibraryVersion="$version"
  LibraryVersion="$version" dotnet format HostSample.slnx --verify-no-changes --no-restore
  dotnet test --solution HostSample.slnx --no-build
)
