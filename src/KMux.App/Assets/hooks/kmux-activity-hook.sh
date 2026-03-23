#!/bin/bash
# KMux Claude Code hook — writes current tool activity to a status file
# so KMux can display it in the pane header.
# Requires: jq

INPUT=$(cat)
PANE_ID="${KMUX_PANE_ID:-}"

[ -z "$PANE_ID" ] && exit 0

TOOL=$(echo "$INPUT"       | jq -r '.tool_name       // empty' 2>/dev/null)
EVENT=$(echo "$INPUT"      | jq -r '.hook_event_name // empty' 2>/dev/null)
SESSION_ID=$(echo "$INPUT" | jq -r '.session_id      // empty' 2>/dev/null)

STATUS_DIR="${TEMP:-${TMPDIR:-/tmp}}/kmux-status"
mkdir -p "$STATUS_DIR"

# Persist session ID whenever we see one (it's stable per Claude Code session)
if [ -n "$SESSION_ID" ]; then
  echo "$SESSION_ID" > "$STATUS_DIR/$PANE_ID-session.txt"
fi

case "$EVENT" in
  PreToolUse)
    echo "busy" > "$STATUS_DIR/$PANE_ID-state.txt"
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
  PostToolUse)
    echo -n "" > "$STATUS_DIR/$PANE_ID.txt"
    ;;
  Stop)
    echo "ready" > "$STATUS_DIR/$PANE_ID-state.txt"
    echo -n "" > "$STATUS_DIR/$PANE_ID.txt"
    ;;
esac

exit 0
