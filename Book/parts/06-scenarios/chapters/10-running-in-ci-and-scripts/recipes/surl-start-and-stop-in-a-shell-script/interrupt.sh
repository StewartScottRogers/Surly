#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Stands in for a CI runner cancelling the job: starts with-surl.sh with a command that would
# run for a minute, sends the script SIGTERM once surl listens, then checks surl is gone.
rm -f surl.out
sh with-surl.sh site sleep 60 2> interrupt.err &
script=$!
until grep -q '^Listening on ' surl.out 2>/dev/null; do sleep 0.1; done
kill "$script"
wait "$script"
echo "with-surl.sh: exit $?"
sh check-stopped.sh
