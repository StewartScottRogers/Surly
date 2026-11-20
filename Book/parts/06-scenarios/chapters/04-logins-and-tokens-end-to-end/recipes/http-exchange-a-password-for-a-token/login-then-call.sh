#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# The client's flow: log in with Basic to fetch a token, then call the API with it as a
# Bearer token. Usage: sh login-then-call.sh <base URL> <user:password>
base=$1
token=$(curl -sSf --cacert server.pem -u "$2" "${base}token.txt") || exit $?
curl -sSf --cacert server.pem --oauth2-bearer "$token" "${base}api/orders.json"
