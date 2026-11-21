#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh peak.sh PORT GLOB [curl options]
# Serves site with every answer held 1 s, so transfers that run together overlap; fetches GLOB
# with the given curl options; then reads surl's trace and prints the most connections open at
# once and the most requests being answered at once.
port=$1; glob=$2; shift 2
rm -f trace.txt listening.txt
surl --directory site --fault "delay;stage=command;ms=1000" --trace-ascii trace.txt http://127.0.0.1:$port/ >listening.txt 2>/dev/null &
pid=$!
until grep -q Listening listening.txt; do sleep 0.1; done
curl -s "$@" "http://127.0.0.1:$port/$glob"
# curl has exited: wait (at most 10 s) until surl has seen every connection close.
tries=0
while [ "$(grep -c ' ended; closing' trace.txt)" != "$(grep -c ' opened: ' trace.txt)" ] && [ $tries -lt 100 ]; do
  sleep 0.1; tries=$((tries + 1))
done
kill $pid
wait $pid 2>/dev/null
awk '/ opened: / { n++; if (n > peak) peak = n } / ended; closing/ { n-- } END { print "connections", peak + 0 }' trace.txt
awk '/held for/ { n++; if (n > peak) peak = n } / \* GET .*: 200/ { n-- } END { print "requests", peak + 0 }' trace.txt
