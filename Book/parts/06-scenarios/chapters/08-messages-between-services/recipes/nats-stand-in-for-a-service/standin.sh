#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Stands in for the price service on prices.get, runs the app's request, and prints what the app got,
# what it asked and what the server logged.
# Usage: sh standin.sh <nats port>
nats=127.0.0.1:$1
rm -f standin.out
{
  printf 'CONNECT {"verbose":false}\r\nSUB prices.get 1\r\nPING\r\n'
  for try in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20; do
    grep -q '^MSG prices.get ' standin.out 2>/dev/null && break
    sleep 0.5
  done
  inbox=$(grep '^MSG prices.get ' standin.out | awk '{ print $4 }')
  printf 'PUB %s 10\r\nprice=9.99\r\n' "$inbox"
  sleep 2
} 2>/dev/null | curl -s -m 30 -T - "telnet://$nats/" > standin.out &
standin=$!
# The server answers the PING once it has the SUB: wait for the PONG, at most five seconds.
for try in 1 2 3 4 5 6 7 8 9 10; do
  grep -q '^PONG' standin.out 2>/dev/null && break
  sleep 0.5
done
echo "app got: $(curl -sS -m 10 -d sku=A1 "nats://$nats/prices.get?request")"
kill $standin 2>/dev/null
tr -d '\r' < standin.out | grep -A 1 '^MSG prices.get ' > asked.txt
inbox=$(awk 'NR == 1 { print $4 }' asked.txt)
echo "the app asked: $(sed -n 2p asked.txt)"
echo "reply subject: $inbox"
# The server's log is written as it works: look for the two PUB lines, at most five seconds.
for try in 1 2 3 4 5 6 7 8 9 10; do
  [ "$(grep -c -e "< PUB prices.get $inbox 6" -e "< PUB $inbox 10" nats.log)" = 2 ] && break
  sleep 0.5
done
echo "server log, the app's request: $(grep -c "< PUB prices.get $inbox 6" nats.log)"
echo "server log, the stand-in's reply: $(grep -c "< PUB $inbox 10" nats.log)"
