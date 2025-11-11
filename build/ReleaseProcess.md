Release process overview

- CI on PRs: Every pull request restores, builds, and tests the solution.
- Promotion: A manual workflow promotes a stable version by bumping `major`, `minor`, or `patch` from `build/VERSION`. All manifest packages are included.

Publishing policy (canonical feed)

- NuGet.org is canonical for public releases.
- Publishing to NuGet.org is required; failure is fatal and aborts the release.
- GitHub Packages is used to light up the GitHub “Packages” sidebar; it is pushed only after NuGet.org publish succeeds.

Versioning

- Base version: `build/VERSION` (e.g., `0.1.0`).
- Stable: computed by bumping `major|minor|patch` from `build/VERSION`. Monotonicity vs latest `vMAJOR.MINOR.PATCH` tag is enforced.

Package manifest

- File: `build/packages.manifest.json`.
- Purpose: Declare publishable packages and define path scopes for change detection.
- Schema:

  {
    "packages": [
      { "id": "<NuGetId>", "path": "<relative/source/path>" }
    ]
  }

- Current entries include (test/sample/toy projects omitted):
  - `Coven.Core` at `src/Coven.Core`
  - `Coven.Core.Streaming` at `src/Coven.Core.Streaming`
  - `Coven.Daemonology` at `src/Coven.Daemonology`
  - `Coven.Transmutation` at `src/Coven.Transmutation`
  - `Coven.Spellcasting` at `src/Coven.Spellcasting`
  - `Coven.Chat` at `src/Coven.Chat`
  - `Coven.Chat.Console` at `src/Coven.Chat.Console`
  - `Coven.Chat.Discord` at `src/Coven.Chat.Discord`
  - `Coven.Agents` at `src/Coven.Agents`
  - `Coven.Agents.OpenAI` at `src/Coven.Agents.OpenAI`


Promotion to stable

- Trigger the “promote” workflow (Actions → promote).
- Choose bump: `patch`, `minor`, or `major`; the version is computed from `build/VERSION`.
- All packages in `build/packages.manifest.json` are packed.
- Monotonic versioning: the workflow ensures the computed version is strictly greater than the latest existing stable tag (`vMAJOR.MINOR.PATCH`).
- NuGet.org (required): packages are pushed to `https://api.nuget.org/v3/index.json` using the `NUGET_API_KEY` secret. If the key is missing or publish fails, the workflow fails and no other destinations are attempted.
- GitHub Packages (secondary): packages are pushed to `https://nuget.pkg.github.com/<owner>/index.json` using the workflow `GITHUB_TOKEN`, but only after a successful NuGet.org publish.

Consumption (GitHub Packages)

- Add a NuGet source pointing to `https://nuget.pkg.github.com/<owner>/index.json`.
- Authenticate with a GitHub token that has `read:packages` scope (PAT) or `${{ secrets.GITHUB_TOKEN }}` in Actions.
- Example `nuget.config` source entry:

  <configuration>
    <packageSources>
      <add key="github" value="https://nuget.pkg.github.com/<owner>/index.json" />
    </packageSources>
  </configuration>

Notes and recommendations

- Keep identical package IDs and versions on both feeds to avoid confusion.
- Prefer linking the NuGet.org page in badges; use GitHub Packages primarily for the GitHub sidebar and internal automation.
