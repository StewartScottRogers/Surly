#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Starts surl on port $1, records each name below into rec/ (the response head in
# <name>.head, the body in <name>), then stops the server. Usage: sh record.sh <port>
url="http://127.0.0.1:$1"
surl -s --directory site "$url/" > /dev/null 2>&1 &
pid=$!
tries=0
until curl -s -o /dev/null --max-time 2 "$url/"; do      # wait until it answers, 10 s at most
  tries=$((tries + 1)); [ "$tries" -ge 50 ] && { kill "$pid"; exit 1; }
  sleep 0.2
done
mkdir -p rec
for name in hello.txt report.csv missing.txt; do
  curl -s --max-time 10 -D "rec/$name.head" -o "rec/$name" "$url/$name"
  echo "recorded $name: $(head -n 1 "rec/$name.head" | tr -d '\r')"
done
kill "$pid"; wait "$pid" 2> /dev/null
curl -s -o /dev/null --max-time 5 "$url/hello.txt"
echo "server stopped: curl exit $?"
