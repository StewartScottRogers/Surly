#!/bin/sh
# Starts the servers test-env.conf describes, waits for them, and tries each one once.
# Any arguments are more surl options, read after the file, so they override it.
set -e
out=$(mktemp)
surl -K test-env.conf "$@" > "$out" &
pid=$!
trap 'kill $pid 2>/dev/null || true; rm -f "$out"' EXIT

# One Listening on line per listen URL once all have bound; if surl exits first, stop with its code.
until [ "$(grep -c '^Listening on ' "$out")" -eq 3 ]; do
  kill -0 $pid 2>/dev/null || { wait $pid; exit $?; }
  sleep 0.1
done
listening() { sed -n "s|^Listening on \($1://.*\)\$|\1|p" "$out"; }

curl -fsS -u tester:secret "$(listening http)hello.txt"
echo "uploaded over FTP" | curl -sS -u tester:secret -T - "$(listening ftp)upload.txt"
curl -fsS -u tester:secret "$(listening http)upload.txt"
printf 'Subject: Ready\r\n\r\nThe environment is up.\r\n' |
  curl -sS -u app@example.com:secret --mail-from app@example.com --mail-rcpt app@example.com \
    -T - "$(listening smtp)example.com"
echo "mail accepted"
