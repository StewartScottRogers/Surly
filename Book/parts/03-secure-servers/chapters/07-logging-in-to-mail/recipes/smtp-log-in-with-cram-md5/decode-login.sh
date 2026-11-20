#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Sends hi.eml once with curl -v on the port given, then decodes the two lines of the CRAM-MD5
# login: the server's challenge and curl's answer to it.
curl -v --crlf -u ann@example.com:annpass --mail-from ann@example.com --mail-rcpt bob@example.com \
  -T hi.eml "smtp://127.0.0.1:$1/example.com" 2>&1 |
  tr -d '\r' | sed -n -e '/^> AUTH CRAM-MD5$/{n;s/^< 334 //p;n;s/^> //p;}' |
  while read -r line; do printf '%s' "$line" | base64 -d; echo; done
