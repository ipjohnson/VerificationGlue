# Releasing

`.github/workflows/release.yml` publishes `VerificationGlue`, `VerificationGlue.Mocks`, `VerificationGlue.Tests` and `VerificationGlue.Tests.AspNetCore` to nuget.org. All four have one version. It is `VersionPrefix` in `Directory.Build.props` with a prerelease label, such as `0.1.0-preview.1`. Each package is a preview, because `VerificationGlue.Tests` depends on the Microsoft.Testing.Platform 3 previews and a stable package cannot depend on a preview.

## Once per repository

The workflow pushes with nuget.org trusted publishing, so no API key is stored in the repository.

1. On nuget.org, open your account menu, choose Trusted Publishing, and add a GitHub Actions policy. Set Repository Owner to `ipjohnson`, Repository to `VerificationGlue` and Workflow File to `release.yml`. Leave Environment empty.
2. Store your nuget.org profile name, not your email address, as a repository secret:

   ```sh
   gh secret set NUGET_USER --repo ipjohnson/VerificationGlue
   ```

A new policy is active for 7 days. The first successful push makes it permanent. Restart the 7 days on nuget.org if no release runs in that time.

## Each release

1. Check that CI passes on `main`.
2. Check that `VersionPrefix` in `Directory.Build.props` is the version to release. Change it in a pull request if not.
3. Tag the commit on `main` with `v`, the version prefix and the label, and push the tag:

   ```sh
   git tag v0.2.0-preview.1 origin/main
   git push origin v0.2.0-preview.1
   ```

The workflow fails when the tag's version prefix differs from `VersionPrefix`. It runs `eng/pack-and-consume.sh` with the label as `VersionSuffix` and pushes the four packages to nuget.org only if the script passes. It then creates a GitHub prerelease for the tag, with the packages attached.

To release without a tag, run the workflow from the Actions tab and enter the label, such as `preview.2`. The workflow then creates the tag with the GitHub release.

A version on nuget.org cannot be deleted or replaced. A bad release is unlisted on nuget.org and followed by the next label. A label with a number, such as `preview.1`, keeps later labels in order. The push skips a package whose version is already on nuget.org, so a failed run can be run again.

## Before a release

- Packing locally with the label shows what the workflow will push:

  ```sh
  eng/pack-and-consume.sh -p:VersionSuffix=preview.1
  ```

  The packages are in `artifacts/pack-and-consume/feed`. Another project can use that folder as a package source to try them before the release.

- A project that references `VerificationGlue.Tests` also needs the `test-tools` feed for the MTP 3 previews. [Getting started with VerificationGlue.Tests](guide/getting-started-tests.md) shows the feed line.
