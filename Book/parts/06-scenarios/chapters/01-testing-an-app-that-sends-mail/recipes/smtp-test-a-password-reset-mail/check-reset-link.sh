#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Reads the reset mail (the INBOX's first message) over IMAP, finds the link in it and fetches it.
# Usage: sh check-reset-link.sh <user:password> <imap port>
body=$(curl -sS -u "$1" "imap://127.0.0.1:$2/INBOX;UID=1;SECTION=TEXT") || exit $?
link=$(printf '%s\n' "$body" | tr -d '\r' | sed -n 's|^.*\(http://[^ ]*/reset/[^ ]*\).*$|\1|p' | head -n 1)
if [ -z "$link" ]; then
  echo "FAIL: no reset link in the mail"
  exit 1
fi
echo "link: $link"
curl -sS -f "$link"
