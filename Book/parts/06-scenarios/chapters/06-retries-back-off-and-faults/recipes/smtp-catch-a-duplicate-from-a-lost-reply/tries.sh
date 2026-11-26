#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh tries.sh TRACE. Waits (at most 10 s) until every exchange in surl's --trace-ascii
# file has ended, then, for each SMTP exchange that reached DATA (a try), prints what the server
# sent from its 354 on, each line after the bytes the server had sent by the end of it, and
# where the server stored the message or cut the connection.
trace=$1
tries=0
until [ "$(grep -c ' opened: ' "$trace")" -le "$(grep -c ' ended; ' "$trace")" ]; do
  tries=$((tries + 1))
  if [ "$tries" -gt 10 ]; then echo "an exchange has not ended" >&2; exit 1; fi
  sleep 1
done
tr -d '\r' < "$trace" | awk '
  / opened: smtp / { smtp[$1] = 1 }
  / => Send data, / { take = smtp[$1] ? $1 : ""; sent[$1] += $5; next }
  / \* Message stored/ && ($1 in try) { print "try " try[$1] ": message stored"; next }
  / cut after / && ($1 in try) { n = $0; sub(/.* cut after /, "", n); sub(/ bytes.*/, "", n); print "try " try[$1] ": cut after " n " bytes"; next }
  /^#/ { take = ""; next }
  take != "" && /^[0-9a-f][0-9a-f][0-9a-f][0-9a-f]: / {
    line = substr($0, 7)
    if (!(take in try) && index(line, "354 ") == 1) try[take] = ++count
    if (take in try) print "try " try[take] ": " sent[take] " " line
  }'
