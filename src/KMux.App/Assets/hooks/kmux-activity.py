#!/usr/bin/env python3
"""KMux activity hook — called from kmux-activity-hook.sh with JSON on stdin."""
import json
import os
import sys

_ACTIVITY_FILE = '.txt'
_STATE_FILE    = '-state.txt'
_SESSION_FILE  = '-session.txt'


def main():
    status_dir = sys.argv[1]
    pane_id    = sys.argv[2]

    try:
        d = json.load(sys.stdin)
    except Exception:
        return

    event      = d.get('hook_event_name', '')
    tool       = d.get('tool_name', '')
    session_id = d.get('session_id', '')
    tool_input = d.get('tool_input', {})

    if session_id:
        _write(status_dir, pane_id + _SESSION_FILE, session_id)

    if event == 'PreToolUse':
        _write(status_dir, pane_id + _STATE_FILE, 'busy')

        if tool in ('Read', 'Write', 'Edit'):
            path = tool_input.get('file_path', '')
            file = os.path.basename(path.replace('\\', '/'))
            activity = f'{tool}ing {file}'
        elif tool == 'Bash':
            cmd = tool_input.get('command', '')[:60]
            activity = f'Running: {cmd}'
        elif tool == 'Grep':
            activity = 'Searching: ' + tool_input.get('pattern', '')
        elif tool == 'Glob':
            activity = 'Finding: ' + tool_input.get('pattern', '')
        elif tool == 'Agent':
            activity = 'Subagent...'
        else:
            activity = tool

        _write(status_dir, pane_id + _ACTIVITY_FILE, activity)

    elif event == 'PostToolUse':
        _write(status_dir, pane_id + _ACTIVITY_FILE, '')

    elif event == 'Stop':
        _write(status_dir, pane_id + _STATE_FILE, 'ready')
        _write(status_dir, pane_id + _ACTIVITY_FILE, '')


def _write(directory, filename, content):
    with open(os.path.join(directory, filename), 'w') as f:
        f.write(content)


if __name__ == '__main__':
    main()
