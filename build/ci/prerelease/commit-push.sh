#!/usr/bin/env bash
# build/ci/prerelease/commit-push.sh
set -euo pipefail

MSG="chore(samples): update to prerelease [skip ci] [prerelease-bot]"
HEAD_REF=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --message) MSG="$2"; shift 2;;
    --head-ref) HEAD_REF="$2"; shift 2;;
    -h|--help) echo "usage: $0 [--message msg] --head-ref branchRef"; exit 0;;
    *) echo "unknown arg: $1"; exit 2;;
  esac
done

[[ -n "$HEAD_REF" ]] || { echo "--head-ref is required"; exit 2; }

if [[ -n "$(git status --porcelain)" ]]; then
  git config user.name  "github-actions[bot]"
  git config user.email "github-actions[bot]@users.noreply.github.com"
  git add -A
  git commit -m "$MSG"
  git push origin HEAD:"$HEAD_REF"
else
  echo "No changes to commit."
fi
