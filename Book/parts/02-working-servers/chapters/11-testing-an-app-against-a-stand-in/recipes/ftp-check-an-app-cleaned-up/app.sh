#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# app.sh FOLDER-URL [leak]: the app under test. It uploads two temporary files to the folder URL (ending in /),
# then deletes both, or only the first when given leak. A failed upload or delete is curl's exit code.
url=$1
curl -sS -T scratch.txt "${url}a.tmp" -T scratch.txt "${url}b.tmp" || exit
if [ "$2" = leak ]; then
  curl -sS -o /dev/null -l -Q "-DELE a.tmp" "$url"
else
  curl -sS -o /dev/null -l -Q "-DELE a.tmp" -Q "-DELE b.tmp" "$url"
fi
