#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# failed-sessions.sh LOG: reads surl's verbose log and prints each session (exchange) that ended on a failure:
# the last command the client sent before it quit or the connection closed, and the reply that ended it, a 4xx
# or 5xx, or "no reply". A line surl logged about that command follows, if there is one. Bytes on a data
# connection are not commands and are skipped. Exit 1 when no session failed.
tr -d '\r' < "$1" | awk '
  { n = substr($1, 2); text = substr($0, length($1) + 4); sub(/\\r\\n$/, "", text) }
  $2 == "*" && $3 == "Data" && $5 == "opened:" { data[n] = 1; next }
  $2 == "*" && $3 == "Data" && $5 == "closed." { data[n] = 0; next }
  $2 == "*" && $3 == "Exchange" && $5 == "ended;" {
    if (cmd[n] != "" && (reply[n] == "" || reply[n] ~ /^[45]/)) {
      print "exchange " n ": " cmd[n] " -> " (reply[n] == "" ? "no reply" : reply[n])
      if (note[n] != "") print "  surl: " note[n]
      failed = 1
    }
    next
  }
  quit[n] { next }
  $2 == "*" && cmd[n] != "" && note[n] == "" { note[n] = text; next }
  $2 == "<" && !data[n] {
    if (text == "QUIT") { quit[n] = 1 } else { cmd[n] = text; reply[n] = ""; note[n] = "" }
    next
  }
  $2 == ">" && cmd[n] != "" && $3 ~ /^[1-5][0-9][0-9]$/ { reply[n] = text }
  END { if (!failed) { print "no failed session in the log"; exit 1 } }'
