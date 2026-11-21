#!/bin/sh
# Usage: sh upload.sh PORT DIR [NAME...]
# Uploads the named files of DIR (all of them when none is named) to /inbox/ on a surl
# whose host key is pinned: the fingerprint comes from host_key.pub, so a different key is refused.
set -e
port=$1; dir=$2; shift 2
[ $# -gt 0 ] || set -- $(ls "$dir")
pin=$(ssh-keygen -l -f host_key.pub | cut -d' ' -f2 | sed 's/^SHA256://')
# One curl for the whole batch: -T takes a glob, and a URL ending in / keeps each file's name.
list=$(echo "$@" | tr ' ' ',')
curl -s -S --hostpubsha256 "$pin" -u alice:s3cret --ftp-create-dirs \
  -T "$dir/{$list}" "sftp://127.0.0.1:$port/inbox/"
