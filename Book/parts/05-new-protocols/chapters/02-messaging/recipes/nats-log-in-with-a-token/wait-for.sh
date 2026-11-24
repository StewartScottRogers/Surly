#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Print every match of a pattern in a log file, once it is there: surl writes an exchange's
# last lines a moment after its client has gone, so wait, at most 10 s, for the first match.
# Usage: sh wait-for.sh <log file> <pattern>
i=0
until grep -q "$2" "$1"; do
  i=$((i+1)); [ $i -ge 100 ] && break; sleep 0.1
done
grep -o "$2" "$1"
