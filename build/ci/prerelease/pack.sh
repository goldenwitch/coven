#!/usr/bin/env bash
# build/ci/prerelease/pack.sh
set -euo pipefail

PATHS=""; VERSION=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --paths) PATHS="$2"; shift 2;;
    --version) VERSION="$2"; shift 2;;
    -h|--help) echo "usage: $0 --paths csv --version semver"; exit 0;;
    *) echo "unknown arg: $1"; exit 2;;
  esac
done

[[ -n "$PATHS" && -n "$VERSION" ]] || { echo "--paths and --version required"; exit 2; }

mkdir -p artifacts/nupkg
IFS=',' read -ra dirs <<< "$PATHS"
shopt -s nullglob

for d in "${dirs[@]}"; do
  d="${d//[$'\r\n']/}"
  [[ -z "$d" ]] && continue
  for csproj in "$d"/*.csproj; do
    echo "Packing: $csproj"
    if grep -qiE '<PackageType>\s*Analyzer\s*</PackageType>|PackagePath="analyzers' "$csproj"; then
      dotnet pack "$csproj" -c Release -o artifacts/nupkg \
        /p:Version="$VERSION" \
        /p:ContinuousIntegrationBuild=true \
        /p:NoWarn=NU5128
    else
      dotnet pack "$csproj" -c Release -o artifacts/nupkg \
        /p:Version="$VERSION" \
        /p:ContinuousIntegrationBuild=true \
        /p:IncludeSymbols=true \
        /p:SymbolPackageFormat=snupkg
    fi
  done
done
