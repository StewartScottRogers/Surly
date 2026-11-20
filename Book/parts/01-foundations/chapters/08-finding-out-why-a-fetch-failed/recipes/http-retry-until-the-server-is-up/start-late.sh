#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Starts curl first and surl two seconds later, as a test that starts both at once may,
# then stops surl once curl is done. Usage: sh start-late.sh <port> [curl options]
port=$1; shift
curl "$@" "http://127.0.0.1:$port/hello.txt" > fetched.txt &
client=$!
sleep 2
surl --directory site "http://127.0.0.1:$port/" > surl.log 2>&1 &
server=$!
wait "$client"
status=$?
kill "$server"
cat fetched.txt
exit $status
