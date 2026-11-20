#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# A CI step: run the tests; if they fail, keep surl's trace in ci-logs/ for the job to
# upload, and fail with the tests' exit code. surl has already withheld the passwords and
# tokens of HTTPS and FTPS from the trace.
# Usage: sh ci-step.sh <test command> [<argument>...]
"$@" && exit 0
status=$?
mkdir -p ci-logs
cp trace.txt ci-logs/trace.txt
exit "$status"
