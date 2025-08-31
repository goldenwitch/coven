#!/usr/bin/env bash
# build/ci/prerelease/detect-changed.sh
set -euo pipefail

usage() {
  cat <<EOF
Usage: $0 --base <sha> --head <sha> [--manifest <path>] [--shared-regex <regex>] [--changed-file-list <file>]
Outputs: any=, ids=, paths= to \$GITHUB_OUTPUT if set; otherwise prints JSON.
EOF
}

BASE=""; HEAD=""; MANIFEST=""; CHANGED_FILE_LIST=""
SHARED_REGEX='^(build/|Build/|Directory\.Build\.|global\.json$|NuGet\.config$|\.config/nuget\.config|\.github/workflows/)'

while [[ $# -gt 0 ]]; do
  case "$1" in
    --base) BASE="$2"; shift 2;;
    --head) HEAD="$2"; shift 2;;
    --manifest) MANIFEST="$2"; shift 2;;
    --shared-regex) SHARED_REGEX="$2"; shift 2;;
    --changed-file-list) CHANGED_FILE_LIST="$2"; shift 2;;
    -h|--help) usage; exit 0;;
    *) echo "Unknown arg: $1"; usage; exit 2;;
  esac
done

# Resolve manifest
if [[ -z "$MANIFEST" ]]; then
  if [[ -f build/packages.manifest.json ]]; then MANIFEST=build/packages.manifest.json
  elif [[ -f Build/packages.manifest.json ]]; then MANIFEST=Build/packages.manifest.json
  else
    echo "packages.manifest.json not found under build/ or Build/." >&2; exit 1
  fi
fi
command -v jq >/dev/null || { echo "jq is required."; exit 1; }

# Gather changed files
if [[ -n "$CHANGED_FILE_LIST" ]]; then
  CHANGED="$(cat "$CHANGED_FILE_LIST" || true)"
else
  [[ -n "$BASE" && -n "$HEAD" ]] || { echo "--base and --head required"; exit 2; }
  CHANGED="$(git diff --name-only "$BASE" "$HEAD" || true)"
fi

# Build id->path map from manifest
mapfile -t MAP < <(jq -r '.packages[] | "\(.id)|\(.path)"' "$MANIFEST")

# Force all if shared inputs changed
if echo "$CHANGED" | grep -E -q "$SHARED_REGEX"; then
  ids="$(printf '%s\n' "${MAP[@]}" | cut -d'|' -f1 | paste -sd, -)"
  paths="$(printf '%s\n' "${MAP[@]}" | cut -d'|' -f2 | paste -sd, -)"
  any=true
else
  ids=""; paths=""
  while IFS= read -r f; do
    for pair in "${MAP[@]}"; do
      id="${pair%%|*}"; pth="${pair##*|}"
      pfx="${pth%/}/"
      if [[ "$f" == "$pfx"* ]]; then
        [[ ",$ids," == *",$id,"* ]] || ids="${ids:+$ids,}$id"
        [[ ",$paths," == *",$pth,"* ]] || paths="${paths:+$paths,}$pth"
      fi
    done
  done <<< "$CHANGED"
  any=$([[ -n "$ids" ]] && echo true || echo false)
fi

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "any=$any"
    echo "ids=$ids"
    echo "paths=$paths"
  } >> "$GITHUB_OUTPUT"
else
  jq -n --arg any "$any" --arg ids "$ids" --arg paths "$paths" '{any:$any, ids:$ids, paths:$paths}'
fi
