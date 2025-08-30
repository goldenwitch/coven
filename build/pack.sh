#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

PRERELEASE="true"
SUFFIX="preview"
RELEASE_VERSION=""
CONFIGURATION="Release"
PATHS=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-prerelease) PRERELEASE="false"; shift ;;
    --suffix) SUFFIX="$2"; shift 2 ;;
    --release-version) RELEASE_VERSION="$2"; PRERELEASE="false"; shift 2 ;;
    -c|--configuration) CONFIGURATION="$2"; shift 2 ;;
    --paths) PATHS="$2"; shift 2 ;;
    *) echo "Unknown arg: $1"; exit 1 ;;
  esac
done

base_version() {
  cat "$SCRIPT_DIR/VERSION" | tr -d '\n' | tr -d '\r'
}

compute_version() {
  if [[ "$PRERELEASE" == "false" ]]; then
    if [[ -z "${RELEASE_VERSION}" ]]; then
      echo "ERROR: --release-version required for stable builds" >&2
      exit 1
    fi
    echo "$RELEASE_VERSION"
    return
  fi
  local base="$(base_version)"
  local run="${GITHUB_RUN_NUMBER:-$(date +%Y%m%d%H%M)}"
  local sha="${GITHUB_SHA:-$(git rev-parse --short HEAD 2>/dev/null || echo local)}"
  sha="${sha:0:7}"
  echo "${base}-${SUFFIX}.${run}+sha.${sha}"
}

version="$(compute_version)"
echo "Packing version ${version} (Prerelease=${PRERELEASE})"

pushd "$REPO_ROOT" >/dev/null
outDir="$REPO_ROOT/artifacts/nupkg"
mkdir -p "$outDir"

dotnet --info >/dev/null
dotnet restore

# Collect projects to pack
declare -a projects
if [[ -n "$PATHS" ]]; then
  IFS=',' read -r -a arr <<< "$PATHS"
  for p in "${arr[@]}"; do
    p_trimmed="${p## }"; p_trimmed="${p_trimmed%% }"
    [[ -z "$p_trimmed" ]] && continue
    if [[ -d "$REPO_ROOT/$p_trimmed" ]]; then
      while IFS= read -r -d '' proj; do projects+=("$proj"); done < <(find "$REPO_ROOT/$p_trimmed" -name '*.csproj' -not -name '*Tests.csproj' -print0)
    fi
  done
else
  while IFS= read -r -d '' proj; do projects+=("$proj"); done < <(find "$REPO_ROOT/src" -name '*.csproj' -not -name '*Tests.csproj' -print0)
fi

# De-duplicate
mapfile -t projects < <(printf '%s\n' "${projects[@]}" | awk '!seen[$0]++')

for proj in "${projects[@]}"; do
  echo "Packing $proj"
  dotnet pack "$proj" -c "$CONFIGURATION" -p:Version="$version" -p:ContinuousIntegrationBuild=true --include-symbols -p:SymbolPackageFormat=snupkg -o "$outDir"
done

echo "Packages written to $outDir"
popd >/dev/null

