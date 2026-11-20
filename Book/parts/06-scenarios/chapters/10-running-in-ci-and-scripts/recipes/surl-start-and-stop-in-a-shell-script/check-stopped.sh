#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Tries the URL of the last surl that with-surl.sh started; curl exit 7 means nothing listens.
curl -s -o /dev/null "$(sed -n 's|^Listening on ||p' surl.out)"
echo "after: curl exit $?"
