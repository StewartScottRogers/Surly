#!/usr/bin/env sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Recipe http-serve-a-directory. The verify script runs this file and compares
# stdout with expected.txt. Lines between the tag markers are shown in the book.
set -e
cd "$(dirname "$0")"
# tag::solution[]
surl --directory site http://127.0.0.1:8080/ &
curl http://127.0.0.1:8080/hello.txt
# end::solution[]
kill %1
