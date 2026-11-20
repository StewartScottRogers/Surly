#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Runs an IMAP SEARCH once a second until it finds a message or the tries run out.
# Usage: sh wait-for-mail.sh <user:password> <search URL> <tries>
tries=$3
while [ "$tries" -gt 0 ]; do
  found=$(curl -sS -u "$1" "$2") || exit $?
  found=$(printf '%s' "$found" | tr -d '\r')
  case $found in
    "* SEARCH "[0-9]*)
      echo "found: UID ${found#\* SEARCH }"
      exit 0 ;;
  esac
  tries=$((tries - 1))
  if [ "$tries" -gt 0 ]; then
    sleep 1
  fi
done
echo "no matching mail after $3 tries"
exit 1
