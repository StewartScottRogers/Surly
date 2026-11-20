#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Stands in for an app that mails a user some seconds after a request: it returns at once
# and sends the message in the background after the delay.
# Usage: sh slow-app.sh <smtp port> <seconds> <recipient> <message file>
(
  sleep "$2"
  curl -s --crlf -u app@example.com:apppass --mail-from app@example.com \
    --mail-rcpt "$3" -T "$4" "smtp://127.0.0.1:$1/example.com"
) > /dev/null 2>&1 &
