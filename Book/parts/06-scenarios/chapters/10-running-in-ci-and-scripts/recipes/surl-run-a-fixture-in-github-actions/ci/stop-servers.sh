#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Step "Stop test servers", run whatever the steps before it did: stop the surl the start step
# left running and wait until it has gone, so its ports are free and its log is complete.
[ -f surl.pid ] || { echo "no surl to stop"; exit 0; }
pid=$(cat surl.pid)
kill "$pid" 2>/dev/null || { echo "surl had already ended"; exit 1; }
tries=0
while kill -0 "$pid" 2>/dev/null; do
  tries=$((tries + 1))
  if [ "$tries" -ge 150 ]; then echo "surl did not stop in 15 s" >&2; exit 1; fi
  sleep 0.1
done
echo "stopped surl"
