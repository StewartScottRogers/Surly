#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Waits, at most ten seconds, until the log holds <count> lines with <text>, then prints them
# without their exchange numbers. Exit 1 if they never come.
# Usage: sh wait-for-log.sh <text> <count> <log file>
tries=0
until [ "$(grep -c -F -- "$1" "$3")" -ge "$2" ]; do
  tries=$((tries + 1))
  [ "$tries" -ge 10 ] && exit 1
  sleep 1
done
grep -F -- "$1" "$3" | sed 's/^#[0-9]* //'
