#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh check.sh <mqtt port> <topic> <expected reading> [tries]
port=$1
topic=$2
expected=$3
tries=${4:-20}
try=0
while :; do
  # The broker sends the topic's length in two bytes, the topic, then the reading: keep what follows.
  kept=$(curl -s "mqtt://127.0.0.1:$port/$topic" | tail -c +$(( ${#topic} + 3 )))
  if [ "$kept" = "$expected" ]; then
    echo "ok: $topic is $kept"
    exit 0
  fi
  try=$(( try + 1 ))
  if [ "$try" -ge "$tries" ]; then
    break
  fi
  sleep 0.5
done
if [ -z "$kept" ]; then
  echo "FAIL: nothing kept on $topic"
else
  echo "FAIL: $topic is $kept, expected $expected"
fi
exit 1
