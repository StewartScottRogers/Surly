#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# The app: answers a request that carries a session ID.
# Usage: sh app.sh <redis port> <http port> <session id>
user=$(curl -s -u app:s3cret "redis://127.0.0.1:$1/GET/session:$3")
if [ -z "$user" ]; then
  echo "logged out: sign in again"
else
  echo "logged in as $user: $(curl -s -u app:s3cret "http://127.0.0.1:$2/account.txt")"
fi
