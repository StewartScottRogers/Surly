#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# app.sh FTP-URL [broken]: the app under test. It writes tonight's export, nightly.csv, and uploads it to
# exports/nightly.csv under the FTP URL (ending in /), making the folder if need be. Given broken, it leaves out
# the last row, as a bug would. A failed upload is curl's exit code.
rows='1,north,42
2,south,17'
if [ "$2" = broken ]; then rows='1,north,42'; fi
printf 'id,region,total\n%s\n' "$rows" > nightly.csv
curl -sS --ftp-create-dirs -T nightly.csv "${1}exports/nightly.csv"
