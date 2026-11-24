#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# The test: a session that expires after six seconds, and the app's answer before and after.
# Usage: sh session.sh <redis port> <http port>
echo "seeded: $(curl -s -u app:s3cret "redis://127.0.0.1:$1/SET/session:abc/alice/EX/6")"
echo "at once: $(sh app.sh "$1" "$2" abc)"
for try in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15; do
  sleep 1
  answer=$(sh app.sh "$1" "$2" abc)
  case $answer in "logged out"*) break ;; esac
done
echo "later: $answer"
