#!/usr/bin/env bash
# Packs the three packages into a local feed and checks them as a user would see them:
#   1. the package layout, one version for all three, and exact dependencies from VerificationGlue.Tests;
#   2. a sample that references only VerificationGlue.Tests builds, publishes under Native AOT with
#      warnings as errors, and runs;
#   3. samples/VerifyAot, which references only VerificationGlue, publishes under Native AOT with
#      warnings as errors and runs. It checks that each message has its shape, file and line;
#   4. the PackageConsumer sample with a different VerificationGlue version fails with VGT0007.
# Extra arguments are passed to dotnet pack, for example -p:VersionSuffix=ci.42 or --no-build.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"
work="$root/artifacts/pack-and-consume"
feed="$work/feed"
mismatch_version="99.0.0"

rm -rf "$work"
mkdir -p "$feed"

fail() { echo "pack-and-consume: $*" >&2; exit 1; }

echo "== Pack"
dotnet pack "$root/VerificationGlue.slnx" -c "$configuration" -o "$feed" "$@"

version=""
for package in VerificationGlue VerificationGlue.Mocks VerificationGlue.Tests; do
  matches=("$feed/$package".[0-9]*.nupkg)
  [ ${#matches[@]} -eq 1 ] && [ -f "${matches[0]}" ] || fail "expected one $package package in $feed"
  package_version="$(basename "${matches[0]}" .nupkg)"
  package_version="${package_version#"$package".}"
  if [ -z "$version" ]; then version="$package_version"; fi
  [ "$package_version" = "$version" ] || fail "$package has version $package_version, expected $version"
done
echo "All three packages have version $version"

echo "== Package layout"
expect_entry() {
  unzip -Z1 "$feed/$1.$version.nupkg" | grep -qx "$2" || fail "$1 does not contain $2"
}
expect_entry VerificationGlue "lib/net10.0/VerificationGlue.dll"
expect_entry VerificationGlue "analyzers/dotnet/cs/VerificationGlue.Generator.dll"
expect_entry VerificationGlue "buildTransitive/VerificationGlue.props"
expect_entry VerificationGlue.Mocks "lib/net10.0/VerificationGlue.Mocks.dll"
expect_entry VerificationGlue.Mocks "analyzers/dotnet/cs/VerificationGlue.Mocks.Generator.dll"
expect_entry VerificationGlue.Mocks "buildTransitive/VerificationGlue.Mocks.props"
expect_entry VerificationGlue.Tests "lib/net10.0/VerificationGlue.Tests.dll"
expect_entry VerificationGlue.Tests "analyzers/dotnet/cs/VerificationGlue.Tests.Generator.dll"

nuspec="$(unzip -p "$feed/VerificationGlue.Tests.$version.nupkg" VerificationGlue.Tests.nuspec)"
for dependency in VerificationGlue VerificationGlue.Mocks; do
  echo "$nuspec" | grep -q "<dependency id=\"$dependency\" version=\"\[$version\]\"" \
    || fail "VerificationGlue.Tests does not depend on exactly $dependency $version"
done
echo "Layout and dependencies are correct"

# A run gets its own package folder and NuGet.config, so a package cached by an earlier run
# with the same version is never used, and the VerificationGlue packages come only from the feed.
write_sample() {
  local dir="$1"
  local name="${2:-PackageConsumer}"
  mkdir -p "$dir"
  cp "$root/samples/$name/"* "$dir/"
  cp "$root/global.json" "$dir/"
  cat > "$dir/NuGet.config" <<CONFIG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local">
      <package pattern="VerificationGlue" />
      <package pattern="VerificationGlue.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
  <config>
    <add key="globalPackagesFolder" value="$dir/packages" />
  </config>
</configuration>
CONFIG
}

echo "== Consume and publish with Native AOT"
sample="$work/sample"
write_sample "$sample"
dotnet publish "$sample/PackageConsumer.csproj" -c Release -warnaserror \
  -p:VerificationGlueVersion="$version" -o "$sample/publish"
[ -x "$sample/publish/PackageConsumer" ] || [ -f "$sample/publish/PackageConsumer.exe" ] \
  || fail "the Native AOT publish produced no executable"
"$sample/publish/PackageConsumer"

echo "== Verify messages under Native AOT"
aot="$work/verify-aot"
write_sample "$aot" VerifyAot
dotnet publish "$aot/VerifyAot.csproj" -c Release -warnaserror \
  -p:VerificationGlueVersion="$version" -o "$aot/publish"
[ -x "$aot/publish/VerifyAot" ] || [ -f "$aot/publish/VerifyAot.exe" ] \
  || fail "the Native AOT publish of VerifyAot produced no executable"
"$aot/publish/VerifyAot" || fail "VerifyAot reported a wrong message under Native AOT"

echo "== Mismatched VerificationGlue version"
# A separate artifacts path keeps this build from overwriting src/VerificationGlue/bin.
dotnet pack "$root/src/VerificationGlue/VerificationGlue.csproj" -c "$configuration" -o "$feed" \
  --artifacts-path "$work/mismatch-build" -p:Version="$mismatch_version"
mismatch="$work/mismatch"
write_sample "$mismatch"
cat > "$mismatch/Directory.Build.targets" <<TARGETS
<Project>
  <ItemGroup>
    <PackageReference Include="VerificationGlue" Version="$mismatch_version" />
  </ItemGroup>
</Project>
TARGETS
set +e
output="$(dotnet build "$mismatch/PackageConsumer.csproj" -c Release -p:VerificationGlueVersion="$version" 2>&1)"
status=$?
set -e
echo "$output"
[ $status -ne 0 ] || fail "the build with VerificationGlue $mismatch_version and VerificationGlue.Tests $version succeeded"
echo "$output" | grep -q "error VGT0007" || fail "the build failed, but not with VGT0007"
echo "The mismatched combination failed with VGT0007"

echo "pack-and-consume: passed"
