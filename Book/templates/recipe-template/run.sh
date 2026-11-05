#!/usr/bin/env sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Recipe KEY. The verify script runs this file and compares stdout with expected.txt.
set -e
cd "$(dirname "$0")"
# tag::solution[]
surl --directory site http://127.0.0.1:8080/ &
curl http://127.0.0.1:8080/hello.txt
# end::solution[]
kill %1
