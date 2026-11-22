#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# fetch-folder.sh URL DIR: downloads every file of the FTP folder URL (ending in /) into DIR, in one
# curl command, and skips the subfolders. Names with spaces are not handled.
url=$1; dir=$2
mkdir -p "$dir"
# The long listing tells folders from files: a folder's line starts with d.
folders=" $(curl -sS "$url" | tr -d '\r' | awk '/^d/ { print $NF }' | tr '\n' ' ')"
set --
for name in $(curl -sS -l "$url" | tr -d '\r'); do
  case "$folders" in
    *" $name "*) echo "skip $name/" ;;
    *) set -- "$@" -o "$dir/$name" "$url$name" ;;
  esac
done
curl -sS --fail-early -w "get %{url.path} %{exitcode}\n" "$@"
