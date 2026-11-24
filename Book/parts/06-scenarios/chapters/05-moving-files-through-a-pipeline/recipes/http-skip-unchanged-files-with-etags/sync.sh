#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# A sync job: fetches each file only when the server's entity tag differs from the one saved with it.
# Usage: sh sync.sh <base URL> <file>...
base=$1; shift
mkdir -p local
for f in "$@"; do
  curl -s -f --etag-compare "local/$f.etag" --etag-save "local/$f.etag" -o "local/$f" -w "%{http_code} $f\n" "$base$f"
done
