#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Writes mailboxes.csv: forty test mailboxes, tester01 to tester40. The last ten log in over IMAP
# only; the rest have an empty protocols field, which means every mail protocol.
echo "address,password,protocols" > mailboxes.csv
i=1
while [ "$i" -le 40 ]; do
  n=$(printf '%02d' "$i")
  if [ "$i" -le 30 ]; then protocols=""; else protocols="imap"; fi
  echo "tester$n@example.com,pass$n,$protocols" >> mailboxes.csv
  i=$((i + 1))
done
