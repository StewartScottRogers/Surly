#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh watch.sh <mqtt port> <mqtt5 port>
topic=mqtt://127.0.0.1:$1/line1/temp
rm -f watched.txt
# The watcher: an MQTT 5 subscriber, which stays subscribed, writing each payload to the file.
curl -s -m 60 "mqtt5://127.0.0.1:$2/line1/temp" -o watched.txt &
watcher=$!
# Wait until it is subscribed: publish a marker until the watcher has written it, at most ten tries.
for try in 1 2 3 4 5 6 7 8 9 10; do
  printf 'watching\n' | curl -s --data-binary @- "$topic"
  grep -q '^watching$' watched.txt 2>/dev/null && break
  sleep 1
done
# The test: the readings the app under test publishes while it runs.
for reading in 21.5 22.0 35.5; do
  printf 'temp=%s\n' "$reading" | curl -s --data-binary @- "$topic"
done
# The end marker: wait until the watcher has written it, at most ten tries, then stop the watcher.
printf 'done\n' | curl -s --data-binary @- "$topic"
for try in 1 2 3 4 5 6 7 8 9 10; do
  grep -q '^done$' watched.txt && break
  sleep 1
done
kill $watcher
grep '^temp=' watched.txt
