#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh share.sh PORT. Two workers subscribe to the shared subscription $share/workers/jobs and a
# monitor subscribes to jobs on its own, each writing what it is sent to a file. Once all three hold a
# ping (the group hands each ping to one worker, so both workers are subscribed), six jobs are published
# to jobs. When the workers hold all six between them, or after 20 s more, it stops the three and prints
# how many jobs each was sent. It gives up after 40 pings.
url="mqtt5://127.0.0.1:$1"
rm -f worker1.txt worker2.txt monitor.txt
curl -s -m 120 "$url/%24share/workers/jobs" -o worker1.txt &
w1=$!
curl -s -m 120 "$url/%24share/workers/jobs" -o worker2.txt &
w2=$!
curl -s -m 120 "$url/jobs" -o monitor.txt &
m=$!
tries=0
until [ -s worker1.txt ] && [ -s worker2.txt ] && [ -s monitor.txt ]; do
  tries=$((tries + 1))
  if [ "$tries" -gt 40 ]; then echo "not all subscribed after 40 pings" >&2; kill "$w1" "$w2" "$m"; exit 1; fi
  printf 'ping\n' | curl -s --data-binary @- "$url/jobs"
  sleep 0.5
done
for n in 1 2 3 4 5 6; do
  printf 'job-%s\n' "$n" | curl -s --data-binary @- "$url/jobs"
done
tries=0
until [ "$(cat worker1.txt worker2.txt | grep -c job)" -ge 6 ] && [ "$(grep -c job monitor.txt)" -ge 6 ]; do
  tries=$((tries + 1))
  if [ "$tries" -gt 40 ]; then break; fi
  sleep 0.5
done
kill "$w1" "$w2" "$m"
echo "worker 1: $(grep -c job worker1.txt) jobs"
echo "worker 2: $(grep -c job worker2.txt) jobs"
echo "monitor: $(grep -c job monitor.txt) jobs"
