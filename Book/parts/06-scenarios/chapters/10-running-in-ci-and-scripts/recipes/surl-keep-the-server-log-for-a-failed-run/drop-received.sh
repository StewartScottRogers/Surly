#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Print a trace without the bytes clients sent: no user names, paths or other client data.
# After a "<= Recv data" line come the bytes a client sent, one offset-led line each, up to
# the next line that starts with an exchange number. Drop those bytes; keep everything else.
# Usage: sh drop-received.sh <trace file>
awk '/^#[0-9]+ <= Recv data/ { print; skip = 1; next }
     /^#[0-9]+ / { skip = 0 }
     !skip' "$1"
