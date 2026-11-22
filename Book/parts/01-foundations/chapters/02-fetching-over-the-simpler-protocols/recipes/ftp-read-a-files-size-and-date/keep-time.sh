#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Downloads the URL in the first argument with the curl options after it, then says
# whether the local copy is older than marker.txt, a file written a moment ago.
url=$1; shift
curl -s "$@" -o copy.txt "$url" || exit
if [ copy.txt -ot marker.txt ]; then echo "copy is older than marker.txt"; else echo "copy is new"; fi
