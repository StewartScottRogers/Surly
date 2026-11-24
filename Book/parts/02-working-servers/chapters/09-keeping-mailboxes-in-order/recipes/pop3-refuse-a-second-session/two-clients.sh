#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Two mail clients on Ann's maildrop at once. The first downloads message 1 slowly, so its
# session stays open; once the server has opened the maildrop for it, the second logs in.
# Usage: sh two-clients.sh <pop3 port>
url="pop3://127.0.0.1:$1"
opened=$(grep -c "Maildrop opened" surl.log)
curl -sS --limit-rate 100 -u ann@example.com:annpass -o first.eml "$url/1" &
first=$!
# Wait, at most ten seconds, until the server's log shows the first session's maildrop opened.
n=0
while [ "$(grep -c "Maildrop opened" surl.log)" -le "$opened" ] && [ $n -lt 50 ]; do sleep 0.2; n=$((n + 1)); done
curl -v -sS -u ann@example.com:annpass "$url/" 2> second.log
echo "second: exit $?"
grep -E "^< -ERR|^curl:" second.log | tr -d '\r'
wait $first
echo "first: exit $?"
