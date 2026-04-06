#!/usr/bin/env bash
set -euo pipefail

ROOT="${1:-.}"
LIVE_DIR="$ROOT/.planning/live"

if [[ ! -d "$LIVE_DIR" ]]; then
  echo "Live coordination directory not found: $LIVE_DIR" >&2
  exit 1
fi

for file in PROTOCOL.md INSTRUCTION.md CODEX_STATUS.md CLAUDE_STATUS.md; do
  path="$LIVE_DIR/$file"
  if [[ -f "$path" ]]; then
    echo "===== $file ====="
    cat "$path"
    echo
  fi
done
