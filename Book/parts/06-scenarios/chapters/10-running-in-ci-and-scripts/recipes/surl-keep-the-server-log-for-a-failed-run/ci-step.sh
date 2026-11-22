#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# A CI step: run the tests; if they fail, keep surl's trace in ci-logs/ for the job to
# upload, and fail with the tests' exit code. surl has already withheld the passwords and
# tokens of HTTPS and FTPS from the trace.
# Usage: sh ci-step.sh <test command> [<argument>...]
"$@" && exit 0
status=$?
# surl traces an exchange's last lines a moment after its client has gone: wait, at most 10 s,
# until every exchange the trace opened has ended.
i=0; until [ "$(grep -c 'Exchange [0-9]* opened: ' trace.txt)" -eq "$(grep -c 'Exchange [0-9]* ended; ' trace.txt)" ]; do i=$((i+1)); [ $i -ge 100 ] && break; sleep 0.1; done
mkdir -p ci-logs
cp trace.txt ci-logs/trace.txt
exit "$status"
