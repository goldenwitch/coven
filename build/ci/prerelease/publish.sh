#!/usr/bin/env bash
# build/ci/prerelease/publish.sh
set -euo pipefail
SRC="https://api.nuget.org/v3/index.json"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --source) SRC="$2"; shift 2;;
    -h|--help) echo "usage: NUGET_API_KEY=... $0 [--source url]"; exit 0;;
    *) echo "unknown arg: $1"; exit 2;;
  esac
done
: "${NUGET_API_KEY:?NUGET_API_KEY is required}"
shopt -s nullglob
for pkg in artifacts/nupkg/*.nupkg; do
  echo "Pushing $pkg"
  dotnet nuget push "$pkg" --skip-duplicate --api-key "$NUGET_API_KEY" --source "$SRC"
done
