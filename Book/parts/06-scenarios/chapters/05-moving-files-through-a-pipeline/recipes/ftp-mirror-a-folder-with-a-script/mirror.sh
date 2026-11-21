#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# mirror.sh FOLDER URL: uploads every file under FOLDER to the FTP folder URL (ending in /) in one
# curl command, making folders as it goes and stopping at the first failure with curl's exit code;
# then lists each folder on the server. Names with spaces or characters a URL cannot hold are not handled.
src=$1; url=$2
set --
for f in $(cd "$src" && find . -type f | sort); do
  set -- "$@" -T "$src/${f#./}" "$url${f#./}"
done
curl -sS --ftp-create-dirs --fail-early -w "put %{url.path} %{exitcode}\n" "$@" || exit
for d in $(cd "$src" && find . -type d | sort); do
  d=${d#.}; d=${d#/}; d=${d:+$d/}
  echo "listing ./$d: $(curl -sS -l "$url$d" | tr -d '\r' | sort | tr '\n' ' ')"
done
