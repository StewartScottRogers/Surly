#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Fetches each URL with curl and saves the session as a HAR (HTTP Archive 1.2) file.
# Usage: sh save-har.sh <file.har> <url>...
har=$1; shift
ms() { awk "BEGIN { printf \"%.3f\", ($1) * 1000 }"; }
pairs() {  # "Name: value" lines in; HAR {"name", "value"} objects out, escaped for JSON
  tr -d '\r' | sed -e '/: /!d' -e 's/\\/\\\\/g' -e 's/"/\\"/g' \
    -e 's/^\([^:]*\): \(.*\)$/{"name": "\1", "value": "\2"}/' | paste -sd, -
}
code=0
{
  echo '{"log": {"version": "1.2", "creator": {"name": "save-har.sh", "version": "1.0"}, "entries": ['
  sep=
  for url in "$@"; do
    started=$(date -u +%Y-%m-%dT%H:%M:%SZ)
    curl -s -v -D head.tmp -o body.tmp "$url" 2> trace.tmp > info.tmp -w '%{method} %{http_version} %{response_code}
%{size_download} %{time_pretransfer} %{time_starttransfer} %{time_total}
%{content_type}' || { code=$?; echo "save-har.sh: curl exited $code on $url" >&2; break; }
    { read -r method version status; read -r size pre start total; read -r type; } <<EOF
$(tr -d '\r' < info.tmp)
EOF
    echo "$sep{\"startedDateTime\": \"$started\", \"time\": $(ms "$total"),"
    echo " \"request\": {\"method\": \"$method\", \"url\": \"$url\", \"httpVersion\": \"HTTP/$version\","
    echo "  \"headers\": [$(sed -n 's/^> //p' trace.tmp | pairs)],"
    echo "  \"cookies\": [], \"queryString\": [], \"headersSize\": -1, \"bodySize\": 0},"
    echo " \"response\": {\"status\": $status, \"statusText\": \"$(head -n 1 head.tmp | tr -d '\r' | cut -d' ' -f3-)\", \"httpVersion\": \"HTTP/$version\","
    echo "  \"headers\": [$(sed 1d head.tmp | pairs)],"
    echo "  \"content\": {\"size\": $size, \"mimeType\": \"$type\", \"encoding\": \"base64\", \"text\": \"$(base64 < body.tmp | tr -d '\n')\"},"
    echo "  \"cookies\": [], \"redirectURL\": \"\", \"headersSize\": -1, \"bodySize\": $size},"
    echo " \"cache\": {}, \"timings\": {\"send\": $(ms "$pre"), \"wait\": $(ms "$start - $pre"), \"receive\": $(ms "$total - $start")}}"
    sep=,
  done
  echo ']}}'
} > "$har.part"
rm -f head.tmp body.tmp trace.tmp info.tmp
if [ "$code" != 0 ]; then rm -f "$har.part"; exit "$code"; fi
mv "$har.part" "$har"
echo "$# requests saved to $har"
