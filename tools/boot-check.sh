#!/usr/bin/env bash
# Boot the game headless with --boot-check: fail on a non-zero exit code, and also on any
# ERROR or unhandled-exception line Godot prints, since those do not change the exit code.
#
#   tools/boot-check.sh
set -euo pipefail

cd "$(dirname "$0")/.."
log=$(mktemp)
trap 'rm -f "$log"' EXIT

status=0
godot-mono --headless --path . -- --boot-check 2>&1 | tee "$log" || status=$?
if [ "$status" -ne 0 ]; then
    echo "boot check: godot exited with $status" >&2
    exit 1
fi
if grep -Eq 'ERROR|Unhandled exception' "$log"; then
    echo "boot check: godot printed an error" >&2
    exit 1
fi
