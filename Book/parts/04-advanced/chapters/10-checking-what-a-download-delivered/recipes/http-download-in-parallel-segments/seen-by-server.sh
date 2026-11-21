#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Reads a surl -v log: the Range headers surl received, the 206 answers it gave, and how
# many connections (exchange numbers) carried them.
# Usage: sh seen-by-server.sh <log>
grep '< Range: ' "$1" | sed -e 's/^#[0-9]* < //' -e 's/\\r\\n$//' | sort -t= -k2n
echo "answered 206: $(grep -c '\* GET /big.bin: 206' "$1")"
echo "connections: $(grep '\* GET /big.bin: 206' "$1" | cut -d' ' -f1 | sort -u | wc -l | tr -d ' ')"
