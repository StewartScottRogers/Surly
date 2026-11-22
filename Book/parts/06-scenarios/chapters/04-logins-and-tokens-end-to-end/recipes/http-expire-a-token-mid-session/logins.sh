#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Polls the control endpoint, at most 25 times 0.2 s apart, until it holds at least <count>
# judged logins, then prints one line per login: account, mechanism, accepted or refused.
# Usage: sh logins.sh <control URL> <count>
auth="Authorization: Bearer $(head -n 1 control-token.txt)"
tries=0
while :; do
  events=$(curl -s -H "$auth" "$1/v1/events?kind=login_judged")
  count=$(printf '%s' "$events" | grep -o '"kind":"login_judged"' | wc -l | tr -d ' ')
  [ "$count" -ge "$2" ] && break
  tries=$((tries + 1))
  [ "$tries" -ge 25 ] && break
  sleep 0.2
done
printf '%s' "$events" | grep -o '"account":"[^"]*","mechanism":"[^"]*","is_accepted":[a-z]*' \
  | sed -e 's/"account":"\([^"]*\)","mechanism":"\([^"]*\)","is_accepted":/\1 \2 /' -e 's/ true$/ accepted/' -e 's/ false$/ refused/'
