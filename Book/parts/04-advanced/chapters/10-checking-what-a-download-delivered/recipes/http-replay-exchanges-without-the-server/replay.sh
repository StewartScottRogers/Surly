#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Fetches the recorded body of $1 with curl from a file: URL, so no server is needed.
# Usage: sh replay.sh <name>
dir=$(cd rec && { pwd -W 2> /dev/null || pwd; })   # pwd -W: the Windows path, in Git Bash
case $dir in /*) base="file://$dir" ;; *) base="file:///$dir" ;; esac
curl -sS --max-time 10 "$(printf '%s/%s' "$base" "$1" | sed 's/ /%20/g')"
