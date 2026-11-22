#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# The reports the client run fetches: site/ has 1 to 50 but 37, mirror/ has 1 to 36.
mkdir -p site/reports mirror/reports
n=1
while [ "$n" -le 50 ]; do
  if [ "$n" -ne 37 ]; then printf 'id,total\n%s,40\n' "$n" > "site/reports/report-$n.csv"; fi
  if [ "$n" -le 36 ]; then cp "site/reports/report-$n.csv" mirror/reports/; fi
  n=$((n + 1))
done
