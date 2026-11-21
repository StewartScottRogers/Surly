#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Fetches <url> as <n> byte ranges at once with one curl command, joins the segments in
# order into joined, and compares the result with <original>.
# Usage: sh fetch-in-segments.sh <url> <n> <original>
url=$1; n=$2; original=$3

# The size, from a HEAD request, and the segment length, rounded up.
size=$(curl -s -f -I -o /dev/null -w "%header{content-length}" "$url") || exit
seg=$(( (size + n - 1) / n ))

# One transfer per segment, separated by --next; the last segment stops at the last byte.
set --
i=0
while [ "$i" -lt "$n" ]; do
  first=$(( i * seg )); last=$(( first + seg - 1 ))
  [ "$last" -lt "$size" ] || last=$(( size - 1 ))
  [ "$i" -eq 0 ] || set -- "$@" --next
  set -- "$@" -s -f -r "$first-$last" -o "segment$i" -w "%{http_code} %header{content-range}\n" "$url"
  i=$(( i + 1 ))
done
curl --parallel --parallel-immediate "$@" > segments.txt || exit
sort -k3n segments.txt

# Join in segment order, not in the order the transfers ended, then compare byte for byte.
: > joined
i=0
while [ "$i" -lt "$n" ]; do cat "segment$i" >> joined; i=$(( i + 1 )); done
echo "joined: $(wc -c < joined | tr -d ' ') bytes"
if cmp -s joined "$original"; then echo "identical"; else echo "differs"; exit 1; fi
