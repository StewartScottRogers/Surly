#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# One test worker: its own surl on a free port over its own directory, its own tests, its own
# stop. Nothing is shared with another worker but the surl binary.
# Usage: sh worker.sh <worker number> [<password the tests log in with>]
n=$1; password=${2:-s3cret}; dir=work/$n
rm -rf "$dir" && mkdir -p "$dir/site"
printf 'worker %s\n' "$n" > "$dir/site/whoami.txt"
: > "$dir/listening.txt"
surl --directory "$dir/site" --user tester:s3cret --allow-uploads http://127.0.0.1:0/ \
  > "$dir/listening.txt" 2> "$dir/surl.log" &
pid=$!

tries=0
until grep -q '^Listening on ' "$dir/listening.txt"; do
  if ! kill -0 "$pid" 2>/dev/null; then wait "$pid"; exit; fi
  tries=$((tries + 1))
  if [ "$tries" -ge 100 ]; then kill "$pid"; exit 1; fi
  sleep 0.1
done
url=$(sed -n 's|^Listening on ||p' "$dir/listening.txt" | tr -d '\r')
echo "$url" | sed 's|.*:\([0-9]*\)/$|\1|' > "$dir/port"

# The tests: upload a result under the name every worker uses, then read it and whoami.txt back.
printf 'result of worker %s\n' "$n" > "$dir/result.txt"
get="curl -fsS --digest -u tester:$password"
$get -T "$dir/result.txt" "${url}result.txt" &&
  result=$($get "${url}result.txt") && whoami=$($get "${url}whoami.txt")
tests=$?
[ "$tests" -ne 0 ] || echo "worker $n: $whoami, $result"

kill "$pid" 2>/dev/null; wait "$pid" 2>/dev/null
exit "$tests"
