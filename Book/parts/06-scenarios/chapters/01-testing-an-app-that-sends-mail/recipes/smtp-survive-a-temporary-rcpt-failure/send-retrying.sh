#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Sends one message with curl, retrying only a temporary (4xx) refusal, at most <tries> tries in all.
# Usage: sh send-retrying.sh <tries> <curl arguments...>
tries=$1
shift
while :; do
  code=$(curl -s -w "%{response_code}" "$@")
  status=$?
  if [ "$status" -eq 0 ]; then
    echo "sent: $code"
    exit 0
  fi
  tries=$((tries - 1))
  case $code in
    4??)
      if [ "$tries" -gt 0 ]; then
        echo "temporary: $code, retrying"
        sleep 1
        continue
      fi ;;
  esac
  echo "gave up: $code"
  exit "$status"
done
