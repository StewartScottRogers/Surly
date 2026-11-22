#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# The client: calls the API with the token it keeps in a file. With no token, it logs in for
# one; when the call is answered 401, it logs in again for a new token and retries once.
# Usage: sh client.sh <base URL> <user:password> <token file>
base=$1
user=$2
cache=$3

login() {
  answer=$(curl -sSf --cacert server.pem -u "$user" "${base}token?service=surl") || exit $?
  old=$(cat "$cache" 2>/dev/null)
  printf '%s' "$answer" | sed 's/^{"token":"\([^"]*\)".*/\1/' > "$cache"
  if [ -z "$old" ]; then echo "login: token issued"
  elif [ "$old" = "$(cat "$cache")" ]; then echo "login: same token"
  else echo "login: new token"; fi
}

call() {
  curl -s --cacert server.pem --oauth2-bearer "$(cat "$cache")" -o api.out -w "%{http_code}" "${base}v2/"
}

test -s "$cache" || login
status=$(call)
echo "call: $status"
if [ "$status" = 401 ]; then
  login
  status=$(call)
  echo "retry: $status"
fi
test "$status" = 200
