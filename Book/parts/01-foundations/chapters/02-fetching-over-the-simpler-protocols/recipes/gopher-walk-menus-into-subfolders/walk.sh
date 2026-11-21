#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh walk.sh PORT. Starts at the root menu, follows the first directory entry
# of each menu until a menu has none, then fetches the first file that menu lists.
base=gopher://127.0.0.1:$1
sel=""
while :; do
  menu=$(curl -s "$base/1$sel" | tr -d '\r')
  next=$(printf '%s\n' "$menu" | awk -F'\t' '/^1/ { print $2; exit }')
  [ -z "$next" ] && break
  echo "menu ${sel:-/} -> $next"
  sel=$next
done
file=$(printf '%s\n' "$menu" | awk -F'\t' '/^0/ { print $2; exit }')
echo "file $file"
curl -s "$base/0$file"
