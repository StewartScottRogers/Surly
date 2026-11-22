#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Stands in for the runner, which a recipe cannot start: runs the workflow's three server
# steps in order, each in a shell of its own; sets what a step appended to $GITHUB_ENV for
# the steps after it; runs the stop step always; and keeps surl.log when a step failed, as
# the upload step would. Exits as the job would: 0, or the first failed step's code.
# Usage: sh steps.sh [<mailbox password the tests use>]
GITHUB_ENV=$PWD/github.env; export GITHUB_ENV
: > "$GITHUB_ENV"
status=0
sh ci/start-servers.sh || status=$?
if [ "$status" -eq 0 ]; then
  set -a; . "$GITHUB_ENV"; set +a
  sh ci/tests.sh "$@" || status=$?
fi
sh ci/stop-servers.sh || [ "$status" -ne 0 ] || status=1
if [ "$status" -ne 0 ]; then mkdir -p artifact && cp surl.log artifact/ && echo "kept surl.log"; fi
exit "$status"
