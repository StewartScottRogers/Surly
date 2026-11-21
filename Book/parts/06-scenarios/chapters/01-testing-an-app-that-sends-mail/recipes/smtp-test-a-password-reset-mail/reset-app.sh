#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Stands in for the app: makes a reset token, publishes the reset page under site/reset/, and
# mails the recipient a link to it. A fourth argument puts another token in the link, as a bug would.
# Usage: sh reset-app.sh <smtp port> <http port> <recipient> [<token for the link>]
token=$(od -An -N8 -tx1 /dev/urandom | tr -d ' \n')
mkdir -p site/reset
printf 'Choose a new password for %s.\n' "$3" > "site/reset/$token"
printf 'From: app@example.com\nTo: %s\nSubject: Reset your password\n\nOpen this link to choose a new password:\nhttp://127.0.0.1:%s/reset/%s\n' \
  "$3" "$2" "${4:-$token}" |
  curl -sS --crlf -u app@example.com:apppass --mail-from app@example.com --mail-rcpt "$3" \
    -T - "smtp://127.0.0.1:$1/example.com"
