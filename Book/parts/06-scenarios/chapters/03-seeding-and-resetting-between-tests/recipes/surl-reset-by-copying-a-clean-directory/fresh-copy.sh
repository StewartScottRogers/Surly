#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Runs one test on a fresh copy of clean/: copies it to data/, starts surl on the copy,
# runs the test, stops surl. Usage: sh fresh-copy.sh <port> [keep]
#   keep  start on data/ as the last test left it, without copying clean/ again
url=http://127.0.0.1:$1/
if [ "$2" != keep ]; then
  rm -rf data && cp -R clean data || exit 1
fi
surl -s --directory data --user tester:secret --allow-uploads "$url" &
pid=$!
tries=0
until curl -s -o /dev/null "$url"; do        # wait until surl answers
  tries=$((tries + 1))
  if [ "$tries" -ge 50 ]; then echo "surl did not start" >&2; kill "$pid"; exit 1; fi
  sleep 0.1
done
# The test: read the counter, write it back one higher, read it again.
n=$(curl -s --digest -u tester:secret "${url}counter.txt")
echo "$n"
echo $((n + 1)) | curl -s --digest -u tester:secret -T - "${url}counter.txt"
curl -s --digest -u tester:secret "${url}counter.txt"
kill "$pid"
wait "$pid" 2>/dev/null
exit 0
