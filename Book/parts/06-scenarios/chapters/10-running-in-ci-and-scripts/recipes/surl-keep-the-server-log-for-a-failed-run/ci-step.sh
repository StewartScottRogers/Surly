#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# A CI step: run the tests; if they fail, keep surl's trace in ci-logs/ for the job to
# upload, with every byte a client sent left out, and fail with the tests' exit code.
# Usage: sh ci-step.sh <test command> [<argument>...]
"$@" && exit 0
status=$?
mkdir -p ci-logs
# After a "<= Recv data" line come the bytes a client sent, one offset-led line each, up to
# the next line that starts with an exchange number. Drop those bytes; keep everything else.
awk '/^#[0-9]+ <= Recv data/ { print; skip = 1; next }
     /^#[0-9]+ / { skip = 0 }
     !skip' trace.txt > ci-logs/trace.txt
exit "$status"
