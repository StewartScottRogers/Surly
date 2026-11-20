#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Downloads a URL and keeps it only if its SHA-256 is the published one.
# Usage: sh fetch-verified.sh <url> <published sha-256> <file>
url=$1 want=$2 file=$3
curl -fsS -o "$file.part" "$url" || exit
if command -v sha256sum >/dev/null; then got=$(sha256sum "$file.part")
else got=$(shasum -a 256 "$file.part"); fi
got=${got%% *}
if [ "$got" != "$want" ]; then
  rm -f "$file.part"
  echo "refused $file: SHA-256 is $got, not $want" >&2
  exit 1
fi
mv "$file.part" "$file"
echo "kept $file: SHA-256 $got"
