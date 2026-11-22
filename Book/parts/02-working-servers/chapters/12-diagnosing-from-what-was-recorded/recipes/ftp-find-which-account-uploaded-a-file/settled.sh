#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# settled.sh LOG: waits until every exchange surl's log says was opened has ended, so the log holds the whole
# of each one. It gives up after 5 seconds, leaving the log as it is.
tries=0
while [ "$(grep -c ' opened: ' "$1")" -gt "$(grep -c ' ended; ' "$1")" ] && [ "$tries" -lt 25 ]; do
  sleep 0.2
  tries=$((tries + 1))
done
