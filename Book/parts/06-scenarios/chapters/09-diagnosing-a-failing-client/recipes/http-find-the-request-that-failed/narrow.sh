#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Narrows surl's verbose log to each answer of 400 or more and the request that caused it.
# Exits 0 when it found one, 1 when every answer was below 400.
# Run: sh narrow.sh [surl.log]    (needs a POSIX shell: Git Bash on Windows)
log=${1:-surl.log}

# The log is whole once every exchange that opened has ended: wait for that, at most 10 s.
tries=0
until [ "$(grep -c ' opened: ' "$log")" -eq "$(grep -c ' ended; ' "$log")" ]; do
  tries=$((tries + 1))
  if [ "$tries" -gt 50 ]; then echo "the exchanges in $log did not end within 10 s"; exit 1; fi
  sleep 0.2
done

# Each log line is '#N <' (received), '#N >' (sent) or '#N *' (an event), then the text.
# Keep the lines of the request being read; on an event that answered 400 or more, print
# them, the event, and the head of the answer that follows (without its Date).
awk '
  { sub(/\r$/, ""); text = $0; sub(/^#[0-9]+ . /, "", text); sub(/\\r\\n$/, "", text) }
  $2 == "<" && $3 ~ /^[A-Z]+$/ && $NF ~ /^HTTP\/1\.[01]/ { requests++; request = "" }
  $2 == "<" && text != "" { request = request "> " text "\n" }
  $2 == "*" && (text ~ /: [45][0-9][0-9], / || text ~ /answered [45][0-9][0-9]/) {
    failed++
    printf "request %d of the run, exchange %s\n%s* %s\n", requests, $1, request, text
    showing = 1; next
  }
  showing && $2 == ">" {
    if (text == "") { showing = 0; print "" } else if (text !~ /^Date: /) print "< " text
  }
  END { printf "%d requests, %d answered 400 or more\n", requests, failed; exit (failed ? 0 : 1) }
' "$log"
