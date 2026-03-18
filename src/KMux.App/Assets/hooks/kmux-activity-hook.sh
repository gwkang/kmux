#!/bin/bash
# KMux Claude Code hook — writes current tool activity to a status file
# so KMux can display it in the pane header.
# Requires: jq

INPUT=$(cat)
TOOL=$(echo "$INPUT"  | jq -r '.tool_name       // empty' 2>/dev/null)
EVENT=$(echo "$INPUT" | jq -r '.hook_event_name // empty' 2>/dev/null)
PANE_ID="${KMUX_PANE_ID:-}"

[ -z "$PANE_ID" ] && exit 0

STATUS_DIR="${TEMP:-${TMPDIR:-/tmp}}/kmux-status"
mkdir -p "$STATUS_DIR"

case "$EVENT" in
  PreToolUse)
    case "$TOOL" in
      Read)   FILE=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty' | xargs -r basename 2>/dev/null); echo "Reading $FILE" ;;
      Write)  FILE=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty' | xargs -r basename 2>/dev/null); echo "Writing $FILE" ;;
      Edit)   FILE=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty' | xargs -r basename 2>/dev/null); echo "Editing $FILE" ;;
      Bash)   CMD=$(echo "$INPUT"  | jq -r '.tool_input.command   // empty' | head -c 60 2>/dev/null);        echo "Running: $CMD" ;;
      Grep)   PAT=$(echo "$INPUT"  | jq -r '.tool_input.pattern   // empty' 2>/dev/null);                     echo "Searching: $PAT" ;;
      Glob)   PAT=$(echo "$INPUT"  | jq -r '.tool_input.pattern   // empty' 2>/dev/null);                     echo "Finding: $PAT" ;;
      Agent)  echo "Subagent..." ;;
      *)      echo "$TOOL" ;;
    esac > "$STATUS_DIR/$PANE_ID.txt"
    ;;
  PostToolUse|PostToolUseFailure)
    echo -n "" > "$STATUS_DIR/$PANE_ID.txt"
    ;;
esac

exit 0
