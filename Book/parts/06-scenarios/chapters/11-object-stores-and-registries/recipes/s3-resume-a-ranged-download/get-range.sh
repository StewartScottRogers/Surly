#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh get-range.sh URL FIRST LAST FILE [curl options...]
# Fetches bytes FIRST to LAST of an object into FILE. When a try ends short, the next one asks
# only for the bytes after those FILE holds; at most 5 tries. Prints each try's Range, status,
# bytes received and curl's exit code, then the bytes fetched in all. An error status (4xx, 5xx)
# is not a lost connection: the script stops there, exit 1.
url=$1 first=$2 last=$3 file=$4
shift 4
: > "$file"
tries=0 fetched=0
while :; do
  have=$(wc -c < "$file" | tr -d ' ')
  from=$((first + have))
  if [ "$from" -gt "$last" ]; then break; fi
  if [ "$tries" -ge 5 ]; then echo "gave up after $tries tries" >&2; exit 1; fi
  tries=$((tries + 1))
  got=$(curl -s -f "$@" -r "$from-$last" -o "$file.try" -w "%{http_code} %{size_download}" "$url")
  code=$?
  echo "bytes=$from-$last: $got, exit $code"
  case "$got" in 4*|5*) exit 1 ;; esac
  fetched=$((fetched + ${got#* }))
  if [ -f "$file.try" ]; then cat "$file.try" >> "$file"; rm -f "$file.try"; fi
done
echo "fetched $fetched bytes for a range of $((last - first + 1))"
