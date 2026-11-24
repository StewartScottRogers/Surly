#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh join.sh <stomp port> <mqtt5 port> <stomp destination> <mqtt topic>
stomp="stomp://127.0.0.1:$1$3"
rm -f got.txt
# The new service: subscribes over MQTT 5, which stays subscribed, and writes each message it is sent.
curl -s -m 60 "mqtt5://127.0.0.1:$2/$4" -o got.txt &
service=$!
# The old service sends over STOMP. A line feed ends each message.
send() {
  printf '%s\n' "$1" | curl -s --data-binary @- "$stomp"
}
# Joined yet? The old service sends ping until the new service has one.
joined=""
for try in 1 2 3 4 5 6 7 8 9 10; do
  send ping
  grep -q '^ping$' got.txt 2>/dev/null && joined=yes && break
  sleep 1
done
if [ -z "$joined" ]; then
  kill $service
  echo "nothing crossed from $3 to $4"
  exit 1
fi
# The test: two orders from the old service, then wait until the last one has crossed.
send "order 1001"
send "order 1002"
for try in 1 2 3 4 5 6 7 8 9 10; do
  grep -q '^order 1002$' got.txt && break
  sleep 1
done
kill $service
grep '^order ' got.txt
