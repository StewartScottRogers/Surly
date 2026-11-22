#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Step "Run tests": fetch the greeting over HTTP and mail it over SMTP, to the URLs the start
# step put in the environment. The first failure ends the step with curl's exit code.
# Usage: sh ci/tests.sh [<mailbox password>]
set -e
greeting=$(curl -fsS "${HTTP_URL}greeting.txt")
printf 'Subject: Greeting\r\n\r\n%s\r\n' "$greeting" |
  curl -sS -u "app@example.com:${1:-secret}" --mail-from app@example.com \
    --mail-rcpt app@example.com -T - "${SMTP_URL}example.com"
echo "mailed: $greeting"
