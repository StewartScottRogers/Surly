#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Stands in for the tests: fetches one path from the server with-surl.sh started.
# Usage: sh tests.sh <path>
curl -fsS "${SURL_URL}$1"
