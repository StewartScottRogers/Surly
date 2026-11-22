#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Reads a surl -v log: the Range headers surl received, the 206 answers it gave, and how
# many connections (exchange numbers) carried them.
# Usage: sh seen-by-server.sh <log>
# surl writes its log a moment after curl is done: wait until every exchange it opened has ended, 10 s at most.
i=0; until [ "$(grep -c '\* Exchange [0-9]* opened' "$1")" = "$(grep -c '\* Exchange [0-9]* ended' "$1")" ]; do i=$((i+1)); [ $i -ge 100 ] && break; sleep 0.1; done
grep '< Range: ' "$1" | sed -e 's/^#[0-9]* < //' -e 's/\\r\\n$//' | sort -t '=' -k2n
echo "answered 206: $(grep -c '\* GET /big.bin: 206' "$1")"
echo "connections: $(grep '\* GET /big.bin: 206' "$1" | cut -d' ' -f1 | sort -u | wc -l | tr -d ' ')"
