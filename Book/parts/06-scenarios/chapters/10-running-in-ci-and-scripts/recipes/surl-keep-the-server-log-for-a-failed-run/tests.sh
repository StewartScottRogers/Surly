#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# The tests of a CI run: the report over HTTPS with a password, then with a token, then
# over FTPS; the first failure ends the run with curl's exit code.
# Usage: sh tests.sh <https port> <ftps port> <ftps password>
set -e
curl -sSf --cacert server.pem -u app:s3cret -o /dev/null "https://127.0.0.1:$1/report.csv"
curl -sSf --cacert server.pem --oauth2-bearer tok-7f3a9c -o /dev/null "https://127.0.0.1:$1/report.csv"
curl -sSf --cacert server.pem -u "app:$3" -o /dev/null "ftps://127.0.0.1:$2/report.csv"
