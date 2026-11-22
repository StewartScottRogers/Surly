#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# For each exchange: the listen URL that took it, from surl's control endpoint, beside what
# the trace says (the scheme, and for HTTP the Host header the client sent).
# Run: sh which-listener.sh <control URL>   (control.hdr holds the token header)
control=$1
count() { curl -s -H @control.hdr "${control}v1/events?kind=$1" | grep -o '"exchange_id"' | grep -c .; }

# The record is whole once every exchange that started has ended: wait, at most 10 s.
tries=0
until [ "$(count exchange_ended)" -eq "$(count exchange_started)" ]; do
  tries=$((tries + 1))
  if [ "$tries" -gt 50 ]; then echo "the exchanges did not end within 10 s"; exit 1; fi
  sleep 0.2
done

curl -s -H @control.hdr "${control}v1/events?kind=exchange_started" |
  grep -o '"exchange_id":[0-9]*,"listen_url":"[^"]*"' | tr -d '"' | sed 's/exchange_id://; s/,listen_url:/ /' |
  while read -r id url; do
    scheme=$(grep "^#$id \* Exchange $id opened: " trace.txt | sed 's/.* opened: \([a-z]*\) from .*/\1/')
    host=$(awk -v mark="#$id " '
      index($0, "#") == 1 { inside = (index($0, mark "<= Recv") == 1); next }
      inside && / Host: / { sub(/.* Host: /, ""); sub(/\r$/, ""); print; exit }' trace.txt)
    echo "exchange $id: $url  trace: $scheme${host:+, Host $host}"
  done
