#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# What a worker meets when it shares with another: starts one surl over shared/ on a free
# port, then a second on the same directory or the same port, and exits with the second's code.
# Usage: sh clash.sh directory|port
: > first.txt
surl --directory shared http://127.0.0.1:0/ > first.txt 2> /dev/null &
first=$!
tries=0
until grep -q '^Listening on ' first.txt; do
  tries=$((tries + 1)); [ "$tries" -lt 100 ] || break; sleep 0.1
done
url=$(sed -n 's|^Listening on ||p' first.txt | tr -d '\r')
if [ "$1" = directory ]; then surl --directory shared http://127.0.0.1:0/; else surl "$url"; fi
code=$?
kill "$first"
exit "$code"
