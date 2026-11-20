#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Fetches one MIME part of a stored message over IMAP, decodes it from base64 and compares
# its bytes with a file. Usage: sh check-attachment.sh <user:password> <part URL> <file>
curl -sS -u "$1" "$2" -o part.b64 || exit $?
if [ ! -s part.b64 ]; then
  echo "no such part"
  exit 1
fi
tr -d '\r' < part.b64 | base64 -d > part.bin || exit 1
if cmp -s part.bin "$3"; then
  echo "attachment matches $3, $(wc -c < "$3" | tr -d ' ') bytes"
else
  echo "attachment differs from $3"
  exit 1
fi
