#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Starts two HTTP servers with the same seed and one account, asks each for a file without a
# login, and says whether the Digest nonces of their 401s are the same.
for n in 1 2; do
  : > "http-$n.txt"
  surl --random-seed 3 --user alice:s3cret http://127.0.0.1:0/ > "http-$n.txt" 2> /dev/null &
  echo $! > "http-$n.pid"
done
for n in 1 2; do
  tries=0
  until grep -q '^Listening on ' "http-$n.txt"; do
    tries=$((tries + 1)); [ "$tries" -lt 100 ] || break; sleep 0.1
  done
  url=$(sed -n 's|^Listening on ||p' "http-$n.txt" | tr -d '\r')
  curl -s -o /dev/null -D - "${url}a.txt" | sed -n 's/.*nonce="\([0-9a-f]*\)".*/\1/p' | head -n 1 > "nonce-$n.txt"
  kill "$(cat "http-$n.pid")"
done
if [ -s nonce-1.txt ] && cmp -s nonce-1.txt nonce-2.txt; then echo "same nonce"; else echo "different nonces"; fi
