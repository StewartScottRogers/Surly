#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Fetches a file busy for one request with one retry after one second, and says whether the
# whole command took at least one second and under thirty.
start=$(date +%s)
curl -s --retry 1 --retry-delay 1 -o /dev/null "$1"
end=$(date +%s)
elapsed=$((end - start))
if [ "$elapsed" -ge 1 ] && [ "$elapsed" -lt 30 ]; then echo "waited at least 1 second"; else echo "took $elapsed seconds"; fi
