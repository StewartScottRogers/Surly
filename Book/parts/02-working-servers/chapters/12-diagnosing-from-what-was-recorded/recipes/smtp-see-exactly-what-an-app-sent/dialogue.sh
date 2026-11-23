#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh dialogue.sh TRACE [FIRST]. Finds the last SMTP exchange in surl's --trace-ascii file,
# waits (at most 10 s) until surl has ended it, then prints its dialogue, one line per protocol
# line: C: for what the client sent, S: for what surl answered. With FIRST, it starts at the
# first client line that begins with FIRST.
trace=$1
n=$(grep 'opened: smtp ' "$trace" | tail -n 1 | sed 's/^#\([0-9]*\) .*/\1/')
tries=0
until grep -q "^#$n \* Exchange $n ended" "$trace"; do
  tries=$((tries + 1))
  if [ "$tries" -gt 10 ]; then echo "exchange $n has not ended" >&2; exit 1; fi
  sleep 1
done
tr -d '\r' < "$trace" | awk -v n="$n" -v first="$2" '
  BEGIN { on = (first == "") }
  index($0, "#" n " <= Recv data") == 1 { who = "C"; next }
  index($0, "#" n " => Send data") == 1 { who = "S"; next }
  /^#/ { who = ""; next }
  who != "" && /^[0-9a-f][0-9a-f][0-9a-f][0-9a-f]: / {
    line = substr($0, 7)
    if (!on && who == "C" && index(line, first) == 1) on = 1
    if (on) print who ": " line
  }'
