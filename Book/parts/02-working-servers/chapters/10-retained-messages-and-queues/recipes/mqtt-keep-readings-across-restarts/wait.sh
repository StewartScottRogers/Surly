#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh wait.sh <mqtt port> <topic> <expected reading>
port=$1
topic=$2
expected=$3
try=0
while [ "$try" -lt 20 ]; do
  # The broker sends the topic's length in two bytes, the topic, then the reading: keep what follows.
  kept=$(curl -s "mqtt://127.0.0.1:$port/$topic" | tail -c +$(( ${#topic} + 3 )))
  if [ "$kept" = "$expected" ]; then
    echo "kept: $topic is $kept"
    exit 0
  fi
  try=$(( try + 1 ))
  sleep 0.5
done
echo "not kept: $topic"
exit 1
