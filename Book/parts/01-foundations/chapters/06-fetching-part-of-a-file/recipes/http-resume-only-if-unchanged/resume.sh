#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Resumes dl/records.txt from the URL in $1, but only onto the version it was started from:
# dl/records.etag holds that version's ETag, saved by the first download.
have=$(wc -c < dl/records.txt | tr -d ' ')
code=$(curl -s -r "$have-" -H "If-Range: $(cat dl/records.etag)" -o dl/rest.part -w '%{http_code}' "$1") || exit
case "$code" in
  206) cat dl/rest.part >> dl/records.txt; rm dl/rest.part; echo "206: appended the missing bytes" ;;
  200) mv dl/rest.part dl/records.txt; echo "200: the file changed; replaced it with the whole new version" ;;
  *) echo "$code: left dl/records.txt alone"; exit 1 ;;
esac
