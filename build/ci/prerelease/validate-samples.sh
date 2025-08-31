#!/usr/bin/env bash
# build/ci/prerelease/validate-samples.sh
set -euo pipefail

if [[ -f samples/Coven.Samples.sln ]]; then
  dotnet restore samples/Coven.Samples.sln -s artifacts/nupkg -s https://api.nuget.org/v3/index.json --nologo
  dotnet build   samples/Coven.Samples.sln -c Release --nologo
else
  mapfile -t PROJS < <(git ls-files 'samples/**/*.csproj' || true)
  if [[ ${#PROJS[@]} -gt 0 ]]; then
    dotnet restore -s artifacts/nupkg -s https://api.nuget.org/v3/index.json --nologo
    for p in "${PROJS[@]}"; do dotnet build "$p" -c Release --nologo; done
  else
    echo "No projects under samples/"; exit 0
  fi
fi

# If you add sample tests later, run dotnet test here.
