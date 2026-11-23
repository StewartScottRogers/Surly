#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh wait-log.sh TEXT LOG. Waits, a second at a time for at most 10, until LOG has a line
# holding TEXT, then prints those lines without their exchange number.
tries=0
until grep -q -e "$1" "$2"; do
  tries=$((tries + 1))
  if [ "$tries" -gt 10 ]; then echo "no line with $1 in $2" >&2; exit 1; fi
  sleep 1
done
grep -e "$1" "$2" | sed 's/^#[0-9]* //' | tr -d '\r'
