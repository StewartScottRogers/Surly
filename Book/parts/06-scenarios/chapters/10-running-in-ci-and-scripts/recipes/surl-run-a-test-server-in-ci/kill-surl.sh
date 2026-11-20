#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Stands in for a test that brings the server down: ends surl at once with SIGKILL.
kill -9 "$(cat surl.pid)"
