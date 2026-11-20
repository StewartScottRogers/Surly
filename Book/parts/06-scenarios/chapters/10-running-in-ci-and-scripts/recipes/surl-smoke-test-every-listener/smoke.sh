#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Smoke-tests listeners: one quick request to each URL in turn, in its own protocol. A
# listener passes if it answers at all, a refusal included. The first that does not answer
# within 3 seconds ends the run with curl's exit code; the rest are not tried.
# Usage: sh smoke.sh <url>...
for url in "$@"; do
  case $url in
    http*) ask= ;;              # a GET; any status will do
    *)     ask="-X NOOP" ;;     # FTP, SMTP, IMAP: a command that changes nothing
  esac
  got=$(curl -s -m 3 $ask -o /dev/null -w '%{response_code} %{num_connects}' "$url")
  status=$?
  code=${got% *}; connects=${got#* }
  if [ "$status" -ne 0 ] && [ "$code" = 000 ]; then
    if [ "$connects" -eq 0 ]; then how="no connection"; else how="connected, no reply"; fi
    echo "FAIL $url: curl exit $status, $how"
    exit "$status"
  fi
  if [ "$code" = 000 ]; then echo "ok ${url%%:*}: curl exit $status"
  else echo "ok ${url%%:*}: curl exit $status, reply $code"; fi
done
