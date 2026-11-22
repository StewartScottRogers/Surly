#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# check.sh FILE-URL EXPECTED: fetches the uploaded file over HTTP into arrived.csv and compares it, byte for
# byte, with EXPECTED. Exit 1 when they differ; a failed fetch (a 404 included, through -f) is curl's exit code.
curl -sS -f -o arrived.csv "$1" || exit
if cmp -s "$2" arrived.csv; then echo "matches $2"; exit 0; fi
echo "differs from $2: expected $(wc -c < "$2" | tr -d ' ') bytes, arrived $(wc -c < arrived.csv | tr -d ' ')"
exit 1
