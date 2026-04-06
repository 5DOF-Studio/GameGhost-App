#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "${1:-.}" && pwd)"
LIVE_DIR="$ROOT/.planning/live"
LOG_FILE="$LIVE_DIR/automation.log"
READY_FLAG="$LIVE_DIR/READY_FOR_CODEX_REVIEW"
MISMATCH_FLAG="$LIVE_DIR/STATUS_MISMATCH"

timestamp() {
  date -u +"%Y-%m-%dT%H:%M:%SZ"
}

log() {
  mkdir -p "$LIVE_DIR"
  printf '[%s] %s\n' "$(timestamp)" "$1" >> "$LOG_FILE"
}

extract_value() {
  local file="$1"
  local prefix="$2"
  awk -v p="$prefix" 'index($0, p) == 1 { sub(p, "", $0); print $0; exit }' "$file"
}

if [[ ! -d "$LIVE_DIR" ]]; then
  echo "Live coordination directory not found: $LIVE_DIR" >&2
  exit 1
fi

INSTRUCTION_FILE="$LIVE_DIR/INSTRUCTION.md"
CLAUDE_STATUS_FILE="$LIVE_DIR/CLAUDE_STATUS.md"
CODEX_STATUS_FILE="$LIVE_DIR/CODEX_STATUS.md"

for file in "$INSTRUCTION_FILE" "$CLAUDE_STATUS_FILE" "$CODEX_STATUS_FILE"; do
  if [[ ! -f "$file" ]]; then
    log "missing file: $file"
    exit 1
  fi
done

claude_phase="$(extract_value "$CLAUDE_STATUS_FILE" "- Current phase/plan: " || true)"
claude_status="$(extract_value "$CLAUDE_STATUS_FILE" "- Current status: " || true)"
instruction_task_line="$(awk '/^Execute Phase / { print; exit }' "$INSTRUCTION_FILE")"

instruction_plan=""
if [[ -n "$instruction_task_line" ]]; then
  instruction_plan="$(printf '%s' "$instruction_task_line" | sed -E 's/^Execute Phase ([0-9]+) Plan ([0-9]+).*/Phase \1 Plan \2/')"
fi

rm -f "$READY_FLAG" "$MISMATCH_FLAG"

if grep -q "ACTIVE" "$INSTRUCTION_FILE"; then
  if [[ "$claude_status" == DONE* ]]; then
    printf '%s\n' "Claude reports completion while active instruction remains assigned." > "$READY_FLAG"
    log "ready_for_review phase=\"$claude_phase\" status=\"$claude_status\""
  fi
fi

if [[ -n "$claude_phase" ]] && [[ -n "$instruction_plan" ]]; then
  if [[ "$claude_phase" != *"$instruction_plan"* ]] && [[ "$claude_status" != DONE* ]]; then
    printf '%s\n' "Claude status does not match current instruction." > "$MISMATCH_FLAG"
    log "status_mismatch instruction=\"$instruction_plan\" claude_phase=\"$claude_phase\" claude_status=\"$claude_status\""
  fi
fi

log "check_complete instruction=\"$instruction_plan\" claude_phase=\"$claude_phase\" claude_status=\"$claude_status\""
