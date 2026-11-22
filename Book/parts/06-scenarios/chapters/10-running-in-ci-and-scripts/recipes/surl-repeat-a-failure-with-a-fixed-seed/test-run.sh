#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# One test run: start surl's S3 endpoint on a free port with a random seed, run the app's
# upload against it, stop surl, and exit with the app's code. The seed is printed first, so a
# failed run's log says how to repeat it.
# Usage: sh test-run.sh [<seed>] [probe]     (no seed: draw one; probe: connect once first)
seed=${1:-$(od -An -N4 -tu4 /dev/urandom | tr -d ' ')}
echo "seed=$seed"
rm -rf store && mkdir -p store/photos
: > listening.txt
surl -v --s3-plaintext --random-seed "$seed" --directory store \
  --user AKIAIOSFODNN7EXAMPLE:s3cret --allow-uploads s3://127.0.0.1:0/ > listening.txt 2> surl.log &
pid=$!

# Wait for the Listening on line; a surl that exits first refused to start.
tries=0
until grep -q '^Listening on ' listening.txt; do
  if ! kill -0 "$pid" 2>/dev/null; then wait "$pid"; code=$?; cat surl.log >&2; exit "$code"; fi
  tries=$((tries + 1))
  if [ "$tries" -ge 100 ]; then kill "$pid"; echo "surl did not start in 10 s" >&2; exit 1; fi
  sleep 0.1
done
url=$(sed -n 's|^Listening on s3://|http://|p' listening.txt | tr -d '\r')

# A readiness check that connects is one more exchange before the app's.
if [ "$2" = probe ]; then curl -s -o /dev/null "$url"; fi

sh upload.sh "$url"
result=$?
kill "$pid" 2>/dev/null; wait "$pid" 2>/dev/null
exit "$result"
