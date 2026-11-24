#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Runs <count> workers side by side, waits for all of them, prints each one's output in order,
# and exits with the first failing worker's code, or 0.
# Usage: sh run-parallel.sh <count> [<worker whose tests use a wrong password>]
count=$1; broken=${2:-0}; pids=
i=1
while [ "$i" -le "$count" ]; do
  password=s3cret; [ "$i" -ne "$broken" ] || password=wrong
  sh worker.sh "$i" "$password" > "out-$i.txt" 2>&1 &
  pids="$pids $!"
  i=$((i + 1))
done

status=0; i=1
for pid in $pids; do
  wait "$pid"; code=$?
  cat "out-$i.txt"
  if [ "$code" -ne 0 ]; then
    echo "worker $i failed: exit $code"
    [ "$status" -ne 0 ] || status=$code
  fi
  i=$((i + 1))
done
exit "$status"
