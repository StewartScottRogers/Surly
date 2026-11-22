#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# who-stored.sh LOG NAME: reads surl's verbose log and prints, for each upload of NAME the server completed,
# the exchange it came in on and the account logged in there, then the account that stored it last.
# An upload the server refused is left out. Exit 1 when the log shows no completed upload of NAME.
tr -d '\r' < "$1" | awk -v name="$2" '
  { n = substr($1, 2) }
  $2 == "*" && $3 == "Login" && $4 == "accepted:" { who[n] = $NF }
  $2 == "<" && $0 == "#" n " < STOR " name "\\r\\n" { asked[n] = 1 }
  $2 == ">" && asked[n] && $3 ~ /^[2-5][0-9][0-9]$/ {
    if ($3 ~ /^2/) { print "exchange " n ": " who[n] " stored " name; last = who[n] }
    asked[n] = 0
  }
  END {
    if (last == "") { print "no completed upload of " name " in the log"; exit 1 }
    print "last stored by " last
  }'
