#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Stands in for a CI runner cancelling the job: starts with-surl.sh with a command that would
# run for a minute, sends the script SIGTERM a second after surl listens, when the command is
# running, then checks surl is gone. It gives up after 10 s if surl never listens.
rm -f surl.out
sh with-surl.sh site sleep 60 2> interrupt.err &
script=$!
tries=0
until grep -q '^Listening on ' surl.out 2>/dev/null; do
  tries=$((tries + 1))
  if [ "$tries" -ge 100 ]; then echo "surl wrote no Listening on line"; kill "$script"; exit 1; fi
  sleep 0.1
done
sleep 1
kill "$script"
wait "$script"
echo "with-surl.sh: exit $?"
sh check-stopped.sh
