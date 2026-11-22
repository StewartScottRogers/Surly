#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Lists the requests a surl --trace-ascii file records: scheme, method, path, status.
# surl traces an exchange's last lines a moment after its client has gone: wait, at most 10 s,
# until every exchange the trace opened has ended.
i=0; until [ "$(grep -c 'Exchange [0-9]* opened: ' "$1")" -eq "$(grep -c 'Exchange [0-9]* ended; ' "$1")" ]; do i=$((i+1)); [ $i -ge 100 ] && break; sleep 0.1; done
awk '/ opened: / { scheme[$1] = $6 }
     / \* GET / { sub(/:$/, "", $4); sub(/,$/, "", $5); print scheme[$1], $3, $4, $5 }' "$1"
