#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Logs in once with curl -v on the port given, then recomputes the APOP digest from what the server sent
# (its timestamp) and the password, and compares it with the digest curl sent.
out=$(curl -sv -u ann@example.com:annpass "pop3://127.0.0.1:$1/" 2>&1 | tr -d '\r')
stamp=$(printf '%s\n' "$out" | sed -n 's/^< +OK surl ready \(<.*>\)$/\1/p')
sent=$(printf '%s\n' "$out" | sed -n 's/^> APOP ann@example.com \([0-9a-f]*\)$/\1/p')
calc=$(printf '%s' "${stamp}annpass" | openssl dgst -md5 -r | cut -d' ' -f1)
if [ -n "$sent" ] && [ "$sent" = "$calc" ]; then echo "curl sent MD5 of the timestamp and the password"; fi
printf 'the password is in the exchange: '
if printf '%s\n' "$out" | grep -q annpass; then echo yes; else echo no; fi
