#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# job.sh FTP-URL: the FTP job. It uploads nightly.csv to outbox/ under the URL (ending in /), then fetches
# inbox/orders.csv. A failed step is curl's exit code, and the job stops there.
curl -sS -T nightly.csv "${1}outbox/nightly.csv" || exit
curl -sS -o orders.csv "${1}inbox/orders.csv"
