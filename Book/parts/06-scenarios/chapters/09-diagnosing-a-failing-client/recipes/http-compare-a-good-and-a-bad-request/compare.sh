#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Puts the first request surl answered 200 beside the last one it answered 400, from its
# verbose log, prints the lines that differ, then the reason surl gave for the 400.
# Run: sh compare.sh [surl.log]    (needs a POSIX shell: Git Bash on Windows)
log=${1:-surl.log}

# The log is whole once every exchange that opened has ended: wait for that, at most 10 s.
tries=0
until [ "$(grep -c ' opened: ' "$log")" -eq "$(grep -c ' ended; ' "$log")" ]; do
  tries=$((tries + 1))
  if [ "$tries" -gt 50 ]; then echo "the exchanges in $log did not end within 10 s"; exit 1; fi
  sleep 0.2
done

# Which exchanges, by the status lines surl sent: #N is the exchange's number.
good=$(grep ' > HTTP/1.1 200 ' "$log" | head -n 1 | cut -d' ' -f1)
bad=$(grep ' > HTTP/1.1 400 ' "$log" | tail -n 1 | cut -d' ' -f1)
if [ -z "$good" ] || [ -z "$bad" ]; then echo "$log has no 200 and 400 to compare"; exit 1; fi
echo "good: exchange $good, answered 200"
echo "bad:  exchange $bad, answered 400"

# Each request as surl read it: its '<' lines, without the prefix and the CR LF shown as text.
grep "^$good < " "$log" | sed -e 's/^#[0-9]* < //' -e 's/\\r\\n$//' > good.txt
grep "^$bad < " "$log" | sed -e 's/^#[0-9]* < //' -e 's/\\r\\n$//' > bad.txt
diff good.txt bad.txt

# Why surl refused it, in its own words.
grep "^$bad \* .*answered 400" "$log" | sed 's/^#[0-9]* \* /why:  /'
exit 0
