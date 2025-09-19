Release process overview

- Prereleases (PRs): On pull requests targeting `main`, we compute a prerelease version and pack only affected packages based on git deltas. Artifacts (`.nupkg/.snupkg`) are uploaded; optional publish to NuGet.org occurs if secrets are available and the PR originates from this repo.
- Promotion: A manual workflow promotes a chosen ref to a stable version and can publish to NuGet.org.

Versioning

- Base version: `build/VERSION` (e.g., `0.1.0`).
- Prerelease: `<base>-preview.pr<PR>.<run>.<shortSha>` (from `build/ci/prerelease/compute-version.sh`). When not on a PR, a date-based variant is used.
- Stable: exact value entered when promoting (e.g., `0.1.1`).

Package manifest

- File: `build/packages.manifest.json`.
- Purpose: Declare publishable packages and define path scopes for change detection.
- Schema:

  {
    "packages": [
      { "id": "<NuGetId>", "path": "<relative/source/path>" }
    ]
  }

- Current entries include `Coven.Core` at `src/Coven.Core`, `Coven.Spellcasting` at `src/Coven.Spellcasting`, and `Coven.Chat` at `src/Coven.Chat`. Test projects are omitted.

Prerelease change detection

1) Determine changed files between the PR base and head SHAs.
2) Mark a package as affected if any changed file is under its manifest `path` (recursive).
3) If shared infra changes are detected (e.g., `build/`, `Build/`, `Directory.Build.*`, `global.json`, NuGet configs, or `.github/workflows/`), treat all manifest packages as affected.
4) If none are affected, the workflow skips packing/publishing. Otherwise, only affected packages are packed and optionally published.

Promotion to stable

- Trigger the “promote” workflow (Actions → promote).
- Version: either provide `version` explicitly or choose a `bump` (patch/minor/major) computed from `build/VERSION`.
- Package selection: provide a comma-separated list of package IDs to promote a subset; omit to promote all manifest packages.
- Monotonic versioning: the workflow ensures the chosen version is strictly greater than the latest existing stable tag (`vMAJOR.MINOR.PATCH`).
- If `NUGET_API_KEY` is set and `publish_nuget: true`, packages are pushed to NuGet.org.

Notes

- CI installs .NET SDK 10 (preview) for `net10.0` targets.
- We no longer mutate package references, update samples, or push commits back from prerelease runs.
