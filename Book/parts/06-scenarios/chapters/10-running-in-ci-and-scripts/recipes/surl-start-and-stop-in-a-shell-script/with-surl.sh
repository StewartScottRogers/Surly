#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Starts surl on a free port to serve <directory>, runs <command> with SURL_URL set to the
# server's URL, and stops surl however the script ends: the command passing or failing, or
# the script itself interrupted. Exits with the command's exit code.
# Usage: sh with-surl.sh <directory> <command> [<argument>...]
dir=$1; shift
: > surl.out
surl --directory "$dir" http://127.0.0.1:0/ > surl.out 2> surl.err &
surl_pid=$!
command_pid=

stop() {   # on any exit: end the command if it still runs, then stop surl and wait for it
  status=$?
  [ -z "$command_pid" ] || kill "$command_pid" 2>/dev/null
  kill "$surl_pid" 2>/dev/null
  wait "$surl_pid"
  echo "stopped surl: exit $?" >&2
  exit "$status"
}
trap stop EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# surl writes "Listening on <url>" once it has bound. If it exits first, it refused to
# start: show why and pass its exit code on.
tries=0
until grep -q '^Listening on ' surl.out; do
  if ! kill -0 "$surl_pid" 2>/dev/null; then
    wait "$surl_pid"; status=$?
    cat surl.err >&2
    trap - EXIT
    exit "$status"
  fi
  tries=$((tries + 1))
  if [ "$tries" -ge 100 ]; then echo "surl wrote no Listening on line" >&2; exit 1; fi
  sleep 0.1
done
SURL_URL=$(sed -n 's|^Listening on ||p' surl.out)
export SURL_URL

# Run the command in the background and wait for it, so a signal to this script is acted on
# at once rather than when the command ends.
"$@" &
command_pid=$!
wait "$command_pid"
status=$?
command_pid=
exit "$status"
