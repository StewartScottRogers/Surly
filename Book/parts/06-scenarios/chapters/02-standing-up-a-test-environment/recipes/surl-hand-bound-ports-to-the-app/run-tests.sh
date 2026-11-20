#!/bin/sh
# Starts surl on free ports, hands the bound URLs to the app, and checks what surl recorded.
# SITE names the directory to serve; it defaults to site.
out=$(mktemp)
log=$(mktemp)
surl -v --directory "${SITE:-site}" --mail-domain example.com --mailbox app@example.com:secret \
  http://127.0.0.1:0/ smtp://127.0.0.1:0/ > "$out" 2> "$log" &
surl_pid=$!
trap 'kill $surl_pid 2>/dev/null; rm -f "$out" "$log"' EXIT

# surl writes one Listening on line per listen URL once every listener has bound.
# If surl exits first, it refused to start: show why and stop with its exit code.
until [ "$(grep -c '^Listening on ' "$out")" -eq 2 ]; do
  kill -0 $surl_pid 2>/dev/null || { wait $surl_pid; code=$?; cat "$log" >&2; exit $code; }
  sleep 0.1
done

APP_HTTP_URL=$(sed -n 's|^Listening on \(http://.*\)$|\1|p' "$out")
APP_SMTP_URL=$(sed -n 's|^Listening on \(smtp://.*\)$|\1|p' "$out")
export APP_HTTP_URL APP_SMTP_URL
echo "the app gets $APP_HTTP_URL and $APP_SMTP_URL"
sh app.sh || exit

# What surl recorded: its log notes each message it stored.
grep -o 'Message stored: .*' "$log"
