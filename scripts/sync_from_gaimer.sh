#!/usr/bin/env bash
set -euo pipefail

REMOTE_NAME="gaimer"
REMOTE_PATH_DEFAULT="/Users/tonynlemadim/Documents/5DOF Projects/gAImer/gAImer_desktop"
TARGET_BRANCH="main"

usage() {
  cat <<USAGE
Usage:
  ./scripts/sync_from_gaimer.sh [--remote-path <path>] [<commit-sha> ...]

Behavior:
  - Ensures a local git remote named 'gaimer' exists.
  - Fetches from 'gaimer'.
  - If no commit SHAs are provided, prints unsynced commits from gaimer/main.
  - If SHAs are provided, cherry-picks them onto current branch with -x.

Examples:
  ./scripts/sync_from_gaimer.sh
  ./scripts/sync_from_gaimer.sh a1b2c3d
  ./scripts/sync_from_gaimer.sh a1b2c3d d4e5f6g
  ./scripts/sync_from_gaimer.sh --remote-path "/custom/path/to/gaimer_desktop" a1b2c3d
USAGE
}

REMOTE_PATH="$REMOTE_PATH_DEFAULT"
COMMITS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --remote-path)
      [[ $# -ge 2 ]] || { echo "Missing value for --remote-path" >&2; exit 1; }
      REMOTE_PATH="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      COMMITS+=("$1")
      shift
      ;;
  esac
done

if ! git rev-parse --git-dir >/dev/null 2>&1; then
  echo "Not inside a git repository." >&2
  exit 1
fi

# Require clean working tree to avoid accidental conflict blending.
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Working tree is not clean. Commit/stash changes before syncing." >&2
  exit 1
fi

if git remote get-url "$REMOTE_NAME" >/dev/null 2>&1; then
  git remote set-url "$REMOTE_NAME" "$REMOTE_PATH"
else
  git remote add "$REMOTE_NAME" "$REMOTE_PATH"
fi

echo "Fetching from $REMOTE_NAME ($REMOTE_PATH)..."
git fetch "$REMOTE_NAME"

if [[ ${#COMMITS[@]} -eq 0 ]]; then
  echo
  echo "Unsynced commits from $REMOTE_NAME/$TARGET_BRANCH -> current branch:"
  git log --oneline --decorate --no-merges "${REMOTE_NAME}/${TARGET_BRANCH}" --not HEAD | sed -n '1,30p'
  echo
  echo "To sync commits:"
  echo "  ./scripts/sync_from_gaimer.sh <commit-sha> [more-shas...]"
  exit 0
fi

echo "Cherry-picking ${#COMMITS[@]} commit(s) with provenance (-x)..."
for sha in "${COMMITS[@]}"; do
  echo "  -> $sha"
  git cherry-pick -x "$sha"
done

echo
echo "Sync complete. Run verification next, for example:"
echo "  dotnet build src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj -f net8.0-maccatalyst -p:EnableCodeSigning=false"
