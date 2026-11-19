#!/usr/bin/env bash
# Packs the four packages into a local feed and checks them as a user would see them:
#   1. the package layout, one version for all four, the license, author, icon and tags in each nuspec,
#      and exact dependencies from VerificationGlue.Tests and VerificationGlue.Tests.AspNetCore;
#   2. samples/PackageConsumer, a test project that references VerificationGlue.Tests, builds, runs and
#      publishes under Native AOT with warnings as errors without the global.json runner setting. dotnet test
#      fails with VGT1001 without the setting and runs the tests with it. The Native AOT binary runs every
#      test, lists them, runs one by UID and writes TRX, and the script records how long the runs take;
#   3. samples/VerifyAot, which references only VerificationGlue, publishes under Native AOT with
#      warnings as errors and runs. It checks that each message has its shape, file and line, at tier 2
#      and, for its [Verify] method, at tier 3;
#   4. samples/MocksAot, which references only VerificationGlue.Mocks, publishes under Native AOT with
#      warnings as errors and runs. It checks each part of the NSubstitute-style setup and the typed surface;
#   5. the PackageConsumer sample with a different VerificationGlue version fails with VGT0007.
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
for package in VerificationGlue VerificationGlue.Mocks VerificationGlue.Tests VerificationGlue.Tests.AspNetCore; do
  matches=("$feed/$package".[0-9]*.nupkg)
  [ ${#matches[@]} -eq 1 ] && [ -f "${matches[0]}" ] || fail "expected one $package package in $feed"
  package_version="$(basename "${matches[0]}" .nupkg)"
  package_version="${package_version#"$package".}"
  if [ -z "$version" ]; then version="$package_version"; fi
  [ "$package_version" = "$version" ] || fail "$package has version $package_version, expected $version"
done
echo "All four packages have version $version"

echo "== Package layout"
expect_entry() {
  # grep -q would stop reading early, and unzip's SIGPIPE then fails the pipe under pipefail.
  unzip -Z1 "$feed/$1.$version.nupkg" | grep -x "$2" > /dev/null || fail "$1 does not contain $2"
}
expect_entry VerificationGlue "lib/net10.0/VerificationGlue.dll"
expect_entry VerificationGlue "analyzers/dotnet/cs/VerificationGlue.Generator.dll"
expect_entry VerificationGlue "analyzers/dotnet/cs/VerificationGlue.CodeFixes.dll"
expect_entry VerificationGlue "buildTransitive/VerificationGlue.props"
expect_entry VerificationGlue "llms.txt"
expect_entry VerificationGlue.Mocks "lib/net10.0/VerificationGlue.Mocks.dll"
expect_entry VerificationGlue.Mocks "analyzers/dotnet/cs/VerificationGlue.Mocks.Generator.dll"
expect_entry VerificationGlue.Mocks "buildTransitive/VerificationGlue.Mocks.props"
expect_entry VerificationGlue.Mocks "buildTransitive/VerificationGlue.Mocks.targets"
expect_entry VerificationGlue.Mocks "README.md"
expect_entry VerificationGlue.Tests "lib/net10.0/VerificationGlue.Tests.dll"
expect_entry VerificationGlue.Tests "analyzers/dotnet/cs/VerificationGlue.Tests.Generator.dll"
expect_entry VerificationGlue.Tests "build/VerificationGlue.Tests.props"
expect_entry VerificationGlue.Tests "build/VerificationGlue.Tests.targets"
expect_entry VerificationGlue.Tests "README.md"
expect_entry VerificationGlue.Tests "llms.txt"
expect_entry VerificationGlue.Tests.AspNetCore "lib/net10.0/VerificationGlue.Tests.AspNetCore.dll"
expect_entry VerificationGlue.Tests.AspNetCore "analyzers/dotnet/cs/VerificationGlue.Tests.AspNetCore.Generator.dll"
expect_entry VerificationGlue.Tests.AspNetCore "README.md"
expect_entry VerificationGlue.Tests.AspNetCore "llms.txt"

for package in VerificationGlue VerificationGlue.Mocks VerificationGlue.Tests VerificationGlue.Tests.AspNetCore; do
  expect_entry "$package" "icon.png"
  nuspec="$(unzip -p "$feed/$package.$version.nupkg" "$package.nuspec")"
  for element in '<license type="expression">MIT</license>' '<icon>icon.png</icon>' '<readme>README.md</readme>' \
                 '<authors>Ian Johnson</authors>' '<tags>'; do
    echo "$nuspec" | grep -F "$element" > /dev/null || fail "the nuspec of $package has no $element"
  done
done

expect_dependency() {
  local nuspec
  nuspec="$(unzip -p "$feed/$1.$version.nupkg" "$1.nuspec")"
  echo "$nuspec" | grep "<dependency id=\"$2\" version=\"$3\"" > /dev/null \
    || fail "$1 does not depend on $2 $3"
}
expect_dependency VerificationGlue.Tests VerificationGlue "\[$version\]"
expect_dependency VerificationGlue.Tests VerificationGlue.Mocks "\[$version\]"
expect_dependency VerificationGlue.Tests.AspNetCore VerificationGlue.Tests "\[$version\]"
unzip -p "$feed/VerificationGlue.Tests.AspNetCore.$version.nupkg" VerificationGlue.Tests.AspNetCore.nuspec \
  | grep '<frameworkReference name="Microsoft.AspNetCore.App"' > /dev/null \
  || fail "VerificationGlue.Tests.AspNetCore has no framework reference to Microsoft.AspNetCore.App"
expect_dependency VerificationGlue.Tests.AspNetCore Microsoft.AspNetCore.TestHost '[^"]*'
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
    <add key="test-tools" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/test-tools/nuget/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local">
      <package pattern="VerificationGlue" />
      <package pattern="VerificationGlue.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
      <!-- The stable Microsoft.Testing.* versions the samples need are on nuget.org; the MTP 3 previews are on test-tools. -->
      <package pattern="Microsoft.Testing.*" />
    </packageSource>
    <packageSource key="test-tools">
      <package pattern="Microsoft.Testing.*" />
    </packageSource>
  </packageSourceMapping>
  <config>
    <add key="globalPackagesFolder" value="$dir/packages" />
  </config>
</configuration>
CONFIG
}

now_ms() { python3 -I -c 'import time; print(int(time.time() * 1000))'; }

echo "== Consume as a test project without the global.json runner setting"
sample="$work/sample"
write_sample "$sample"
python3 -I -c 'import json, sys; g = json.load(open(sys.argv[1])); g.pop("test", None); json.dump(g, open(sys.argv[2], "w"), indent=2)' \
  "$root/global.json" "$sample/global.json"
grep -q '"runner"' "$sample/global.json" && fail "the sample's global.json still selects a test runner"
mtp_version="$(sed -n 's/.*Include="Microsoft.Testing.Platform" Version="\([^"]*\)".*/\1/p' "$root/Directory.Packages.props")"
grep -q "Include=\"Microsoft.Testing.Extensions.TrxReport\" Version=\"$mtp_version\"" "$sample/PackageConsumer.csproj" \
  || fail "samples/PackageConsumer does not reference Microsoft.Testing.Extensions.TrxReport $mtp_version, the MTP version in Directory.Packages.props"

cd "$sample"
# The publish comes first, because it restores for the runtime identifier and a later restore without one keeps working.
dotnet publish PackageConsumer.csproj -c Release -warnaserror -p:VerificationGlueVersion="$version" -o "$sample/publish"
[ -x "$sample/publish/PackageConsumer" ] || [ -f "$sample/publish/PackageConsumer.exe" ] \
  || fail "the Native AOT publish produced no executable"
dotnet build PackageConsumer.csproj -c Release -warnaserror -p:VerificationGlueVersion="$version"
dotnet run --project PackageConsumer.csproj -c Release --no-build --property:VerificationGlueVersion="$version"

set +e
output="$(dotnet test PackageConsumer.csproj -c Release --no-build -p:VerificationGlueVersion="$version" 2>&1)"
status=$?
set -e
echo "$output"
[ $status -ne 0 ] || fail "dotnet test without the runner setting succeeded"
echo "$output" | grep -q "error VGT1001" || fail "dotnet test without the runner setting failed, but not with VGT1001"
echo "dotnet test without the runner setting failed with VGT1001"

echo "== Run the tests under Native AOT"
"$sample/publish/PackageConsumer" || fail "the tests failed under Native AOT"
"$sample/publish/PackageConsumer" --list-tests | grep -q "found 6 test(s)" || fail "the Native AOT binary did not list 6 tests"
"$sample/publish/PackageConsumer" --filter-uid "PackageConsumer.AotTests.Runs_a_test()" | grep -q "total: 1" \
  || fail "--filter-uid did not select exactly one test under Native AOT"
"$sample/publish/PackageConsumer" --report-trx --report-trx-filename aot.trx --results-directory "$sample/results" > /dev/null
[ "$(grep -o 'outcome="Passed"' "$sample/results/aot.trx" | wc -l | tr -d ' ')" = "6" ] \
  || fail "the TRX report from the Native AOT binary does not show 6 passed tests"

# Timed after the runs above, so the first launch of each binary is not part of the figure.
"$sample/bin/Release/net10.0/PackageConsumer" > /dev/null || fail "the tests failed before the Native AOT publish"
jit_start="$(now_ms)"
"$sample/bin/Release/net10.0/PackageConsumer" > /dev/null
jit_ms=$(( $(now_ms) - jit_start ))
aot_start="$(now_ms)"
"$sample/publish/PackageConsumer" > /dev/null
aot_ms=$(( $(now_ms) - aot_start ))

summary="Native AOT test run of samples/PackageConsumer: ${aot_ms} ms for 6 tests, from process start to exit. The same tests on the JIT took ${jit_ms} ms."
echo "$summary"
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then echo "$summary" >> "$GITHUB_STEP_SUMMARY"; fi

echo "== dotnet test with the runner setting"
cp "$root/global.json" "$sample/global.json"
dotnet test --project PackageConsumer.csproj -c Release --no-build -p:VerificationGlueVersion="$version"
cd "$root"

echo "== Verify messages under Native AOT"
aot="$work/verify-aot"
write_sample "$aot" VerifyAot
dotnet publish "$aot/VerifyAot.csproj" -c Release -warnaserror \
  -p:VerificationGlueVersion="$version" -o "$aot/publish"
[ -x "$aot/publish/VerifyAot" ] || [ -f "$aot/publish/VerifyAot.exe" ] \
  || fail "the Native AOT publish of VerifyAot produced no executable"
"$aot/publish/VerifyAot" || fail "VerifyAot reported a wrong message under Native AOT"

echo "== Mocks under Native AOT"
mocks="$work/mocks-aot"
write_sample "$mocks" MocksAot
dotnet publish "$mocks/MocksAot.csproj" -c Release -warnaserror \
  -p:VerificationGlueVersion="$version" -o "$mocks/publish"
[ -x "$mocks/publish/MocksAot" ] || [ -f "$mocks/publish/MocksAot.exe" ] \
  || fail "the Native AOT publish of MocksAot produced no executable"
"$mocks/publish/MocksAot" || fail "MocksAot found a mock feature that misbehaves under Native AOT"

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
