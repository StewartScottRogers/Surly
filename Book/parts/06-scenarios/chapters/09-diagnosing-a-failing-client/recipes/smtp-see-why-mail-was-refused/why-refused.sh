#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh why-refused.sh LOG. Finds the last SMTP exchange in surl's -v log, waits (at most
# 10 s) until surl has ended it, then prints each refusal in it, a reply of 4xx or 5xx, with
# the two lines before it: the command and surl's reason.
log=$1
n=$(grep 'opened: smtp' "$log" | tail -n 1 | sed 's/^#\([0-9]*\) .*/\1/')
tries=0
until grep -q "^#$n \* Exchange $n ended" "$log"; do
  tries=$((tries + 1))
  if [ "$tries" -gt 10 ]; then echo "exchange $n has not ended" >&2; exit 1; fi
  sleep 1
done
grep -B 2 -E "^#$n > [45][0-9][0-9] " "$log" | grep "^#$n " | sed -e 's/^#[0-9]* //' -e 's/\\r\\n$//'
