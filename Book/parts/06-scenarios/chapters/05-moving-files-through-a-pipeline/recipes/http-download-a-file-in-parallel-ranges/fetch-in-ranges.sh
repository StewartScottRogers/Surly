#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Fetches a file as four byte ranges at once, then joins and checks them.
# Usage: sh fetch-in-ranges.sh <url>
url=$1

# The size, from a HEAD request.
size=$(curl -s -f -I -o /dev/null -w "%header{content-length}" "$url")
part=$(( (size + 3) / 4 ))

# Four ranges, the last one cut at the end of the file; one curl, four transfers at once.
r0="0-$(( part - 1 ))"
r1="$part-$(( 2 * part - 1 ))"
r2="$(( 2 * part ))-$(( 3 * part - 1 ))"
r3="$(( 3 * part ))-$(( size - 1 ))"
w="%{http_code} %header{content-range} %{num_connects}\n"
curl --parallel --parallel-immediate \
  -s -f -w "$w" -r "$r0" -o part0 "$url" --next \
  -s -f -w "$w" -r "$r1" -o part1 "$url" --next \
  -s -f -w "$w" -r "$r2" -o part2 "$url" --next \
  -s -f -w "$w" -r "$r3" -o part3 "$url" > ranges.txt || exit
sort ranges.txt

sh check.sh part0 part1 part2 part3
