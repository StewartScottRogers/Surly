#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Stands in for a crash: kills the surl that on-a-socket.sh started, outright, and waits
# until it is gone.
kill -9 "$SURL_PID"
while kill -0 "$SURL_PID" 2>/dev/null; do sleep 0.1; done
