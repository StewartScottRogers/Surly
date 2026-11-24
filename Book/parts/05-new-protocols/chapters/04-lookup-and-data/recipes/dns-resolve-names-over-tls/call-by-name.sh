#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh call-by-name.sh DNS-PORT NAME HTTP-PORT PATH. Looks NAME up over DNS over TLS on
# 127.0.0.1:DNS-PORT, trusting server.pem, then fetches http://NAME:HTTP-PORT/PATH from the
# address the answer gave. A failed lookup exits with curl's code; no A record exits 6.
answer=$(curl -sS --max-time 10 --dns-transport tls --cacert server.pem "dns://127.0.0.1:$1/$2") || exit
addr=$(printf '%s\n' "$answer" | awk -F '\t' 'NF == 5 && $4 == "A" { print $5; exit }')
if [ -z "$addr" ]; then echo "$2 did not resolve" >&2; exit 6; fi
echo "$2 is $addr"
curl -sS --max-time 10 --resolve "$2:$3:$addr" "http://$2:$3$4"
