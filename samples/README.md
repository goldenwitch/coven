Samples

Overview

- Purpose: Host runnable, focused examples demonstrating Coven concepts without affecting packaging or releases.
- Target: net10.0 to match the main solution.
- Packaging: All samples are non-packable and excluded from release workflows.

Structure

- samples/
  - Coven.Samples.sln        # Opens all samples side-by-side
  - Directory.Build.props    # Common settings for all samples
  - GettingStarted/          # Category for simpler samples with local machine testable results.
    - 01.LocalCodexCLI       # Sample demonstrating using Coven with a console app to extend Codex CLI
  - DeveloperTools/          # Category for code productivity implementations.
  - Science/                 # Category for academic focused work targeting cutting edge solutions.

Per-sample layout

- samples/<Category>/<SampleName>/
  - <SampleName>.sln                     # Individual solution for just this sample
  - src/                                 # One or more projects for the sample
    - <SampleName>.Console.csproj        # Example project name
  - README.md                            # Run instructions and prerequisites

Guidelines

- Project references: Prefer referencing projects from ../src instead of NuGet packages during development.
- IsPackable: Ensure sample projects are not packable; inherited via Directory.Build.props.
- Analyzer Demo: May include intentional warnings; do not set TreatWarningsAsErrors for that sample.
- External services: Gate optional dependencies (e.g., Discord token, Codex CLI) behind environment variable checks and provide fallback behavior or friendly messages.

Contributing a sample

1) Choose a category folder or create a new one if appropriate.
2) Create a new folder `samples/<Category>/<SampleName>/`.
3) Add a `<SampleName>.sln` including only the sample’s projects.
4) Place one or more projects under `src/` and reference `../..../../src/*` projects as needed.
5) Write a `README.md` with quick start and prerequisites.
6) Open `samples/Coven.Samples.sln` to see your sample side-by-side with others.

