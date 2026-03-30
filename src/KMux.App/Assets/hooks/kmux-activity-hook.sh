#!/bin/bash
# KMux Claude Code hook — writes current tool activity to a status file
# so KMux can display it in the pane header.

PANE_ID="${KMUX_PANE_ID:-}"
[ -z "$PANE_ID" ] && exit 0

STATUS_DIR="${TEMP:-${TMPDIR:-/tmp}}/kmux-status"
mkdir -p "$STATUS_DIR"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
python "$SCRIPT_DIR/kmux-activity.py" "$STATUS_DIR" "$PANE_ID"

exit 0
