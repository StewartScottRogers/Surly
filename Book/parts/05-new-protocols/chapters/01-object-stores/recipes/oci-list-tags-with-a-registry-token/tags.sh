#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Lists a repository's tags: logs in at the registry's token endpoint with Basic for a token
# scoped to pull the repository, then asks for the tag list with that token. Prints the status
# and the body, and the Link header when there is a next page.
# Usage: sh tags.sh REGISTRY USER:PASSWORD REPOSITORY [QUERY [SCOPE]]
base=$1
repo=$3
scope=${5:-repository:$repo:pull}
answer=$(curl -sSf --cacert server.pem -u "$2" "${base}token?service=surl&scope=$scope") || exit $?
token=$(printf '%s' "$answer" | sed 's/^{"token":"\([^"]*\)".*/\1/')
code=$(curl -s --cacert server.pem --oauth2-bearer "$token" -D tags.headers -o tags.json -w "%{http_code}" "${base}v2/$repo/tags/list${4:+?$4}")
echo "$code $(cat tags.json)"
tr -d '\r' < tags.headers | grep -i '^link:'
test "$code" = 200
