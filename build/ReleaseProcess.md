Release process overview

- Pre-releases: On pushes to `main`, we only create a prerelease when changes affect packages listed in our manifest. The workflow packs just the affected packages and creates a GitHub prerelease tagged `v<base>-preview.<run>+sha.<shortSha>` with attached `.nupkg/.snupkg`.
- Promotion: Use a manual workflow to promote to a stable release. It rebuilds from the chosen ref with the provided version and may publish to NuGet.org.

Versioning

- Base version: `Build/VERSION` (e.g., `0.1.0`).
- Prerelease: `<base>-preview.<run>+sha.<shortSha>`.
- Stable: exact value entered when promoting (e.g., `0.1.0`).

Package manifest

- File: `Build/packages.manifest.json`.
- Purpose: Declare which packages are publishable and define the path scope used for change detection.
- Schema:

  {
    "packages": [
      { "id": "<NuGetId>", "path": "<relative/source/path>" }
    ]
  }

- Example entries include `Coven.Core` at `src/Coven.Core`, `Coven.Spellcasting` at `src/Coven.Spellcasting`, etc. Tests are omitted.

Pre-release change detection

1) Determine changed files for the push to `main` (the merge commit of a PR).
2) Mark a package as affected if any changed file is under its `path` (recursive).
3) If no packages are affected, skip prerelease/tag creation for that run.
4) If any are affected, pack only those packages and create/update the prerelease with the computed version.

Promotion to stable

- Trigger the “promote” workflow (Actions → promote).
- Version: either provide `version` explicitly or choose a `bump` (patch/minor/major) to compute from `Build/VERSION`.
- Package selection: provide a comma-separated list of package IDs via the `packages` input to promote a subset; omit to promote all manifest packages.
- Monotonic versioning: the workflow validates that the chosen version is strictly greater than the latest existing stable tag (`vMAJOR.MINOR.PATCH`). If not, the job fails and nothing is published.
- If `NUGET_API_KEY` is present and `publish_nuget: true`, packages are pushed to NuGet.org.

Local packing

- Prerelease build: `./Build/pack.sh` or `./Build/pack.ps1` (packs all publishable projects when run locally).
- Stable build: `./Build/pack.sh --no-prerelease --release-version 0.1.0` or `./Build/pack.ps1 -PreRelease:$false -ReleaseVersion 0.1.0`.

Notes

- CI installs .NET SDK 10 (preview) for `net10.0` targets.
- Test projects are excluded and should not be listed in the manifest.
