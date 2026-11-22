#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Step "Start test servers": start surl in the background on free ports for HTTP and SMTP,
# keep its process ID in surl.pid for the stop step, and hand the URLs to later steps by
# appending them to the file $GITHUB_ENV names. Fails the step if surl does not start.
: > listening.txt
surl -v --log-file surl.log --directory site --mail-domain example.com \
  --mailbox app@example.com:secret http://127.0.0.1:0/ smtp://127.0.0.1:0/ \
  < /dev/null > listening.txt 2> surl.err &
pid=$!
echo "$pid" > surl.pid

tries=0
until [ "$(grep -c '^Listening on ' listening.txt)" -eq 2 ]; do
  if ! kill -0 "$pid" 2>/dev/null; then
    wait "$pid"; code=$?
    cat surl.err >&2
    echo "surl did not start: exit $code" >&2
    exit "$code"
  fi
  tries=$((tries + 1))
  if [ "$tries" -ge 100 ]; then kill "$pid"; echo "surl did not start in 10 s" >&2; exit 1; fi
  sleep 0.1
done
sed -n -e 's|^Listening on \(http://.*\)|HTTP_URL=\1|p' \
       -e 's|^Listening on \(smtp://.*\)|SMTP_URL=\1|p' listening.txt | tr -d '\r' >> "$GITHUB_ENV"
