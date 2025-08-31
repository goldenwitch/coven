#!/usr/bin/env bash
# build/ci/prerelease/compute-version.sh
set -euo pipefail

LABEL="preview"; PR=""; RUN=""; SHA=""
VERFILE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --pr) PR="$2"; shift 2;;
    --run) RUN="$2"; shift 2;;
    --sha) SHA="$2"; shift 2;;
    --label) LABEL="$2"; shift 2;;
    --version-file) VERFILE="$2"; shift 2;;
    -h|--help) echo "usage: $0 --pr N --run N --sha sha [--label preview] [--version-file path]"; exit 0;;
    *) echo "unknown arg: $1"; exit 2;;
  esac
done

if [[ -z "$VERFILE" ]]; then
  if [[ -f build/VERSION ]]; then VERFILE=build/VERSION
  elif [[ -f Build/VERSION ]]; then VERFILE=Build/VERSION
  else echo "VERSION file not found."; exit 1; fi
fi

base="$(tr -d '\r\n' < "$VERFILE")"
base="${base%%-*}" # strip any existing pre-release
short="${SHA:0:7}"
: "${RUN:=0}"

if [[ -n "$PR" ]]; then
  version="${base}-${LABEL}.pr${PR}.${RUN}.${short}"
else
  # Fallback shape when not on PR
  stamp="$(date -u +%Y%m%d)"
  version="${base}-${LABEL}.${stamp}.${RUN}.${short}"
fi

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then echo "version=$version" >> "$GITHUB_OUTPUT"; else echo "$version"; fi
