#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Starts surl on a free port, waits for its Listening on line, reads the port from it,
# fetches hello.txt with curl, and stops surl. Arguments, if given, replace surl's.
[ $# -gt 0 ] || set -- --directory site http://127.0.0.1:0/
surl "$@" > listening.txt 2> surl.log &
pid=$!
tries=0
until grep -q '^Listening on ' listening.txt; do
  if ! kill -0 "$pid" 2>/dev/null; then   # surl gave up: show why and pass on its code
    wait "$pid"; code=$?; cat surl.log >&2; exit $code
  fi
  tries=$((tries + 1))
  if [ "$tries" -ge 50 ]; then echo "no Listening on line from surl" >&2; kill "$pid"; exit 1; fi
  sleep 0.1
done
url=$(sed -n 's|^Listening on ||p' listening.txt | head -n 1)
port=${url##*:}; port=${port%/}
echo "port $port"
curl -s "${url}hello.txt"
status=$?
kill "$pid"
exit $status
