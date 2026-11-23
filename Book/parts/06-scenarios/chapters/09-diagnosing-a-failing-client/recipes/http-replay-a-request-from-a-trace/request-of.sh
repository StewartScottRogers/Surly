#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Writes to standard output the bytes one exchange received, as surl's --trace dumped them:
# the hex columns of that exchange's "<= Recv data" blocks, turned back into bytes.
# WHICH is the exchange's number, or text from one of surl's own lines about it, such as
# MalformedHeaderField; then the last exchange with such a line is the one taken.
# Run: sh request-of.sh trace.txt WHICH > request.http   (needs a POSIX shell: Git Bash on Windows)
trace=$1
which=$2
case $which in
  '' | *[!0-9]*) n=$(grep '^#[0-9]* \* ' "$trace" | grep -F -- "$which" | tail -n 1 | cut -d' ' -f1 | tr -d '#') ;;
  *) n=$which ;;
esac
if [ -z "$n" ]; then echo "no exchange in $trace has a line with $which" >&2; exit 1; fi

# A data line is an offset, then up to 16 bytes as two hex digits each, then the same bytes as text.
LC_ALL=C awk -v n="#$n" '
  function hex(h) { return (index("0123456789abcdef", substr(h, 1, 1)) - 1) * 16 + index("0123456789abcdef", substr(h, 2, 1)) - 1 }
  $1 == n { received = ($2 == "<="); next }
  /^#/ { received = 0; next }
  received { for (i = 2; i <= 17 && $i ~ /^[0-9a-f][0-9a-f]$/; i++) printf "%c", hex($i) }
' "$trace"
