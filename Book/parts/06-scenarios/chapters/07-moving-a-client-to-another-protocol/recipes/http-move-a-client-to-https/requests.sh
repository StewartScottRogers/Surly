#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Lists the requests a surl --trace-ascii file records: scheme, method, path, status.
awk '/ opened: / { scheme[$1] = $6 }
     / \* GET / { sub(/:$/, "", $4); sub(/,$/, "", $5); print scheme[$1], $3, $4, $5 }' "$1"
