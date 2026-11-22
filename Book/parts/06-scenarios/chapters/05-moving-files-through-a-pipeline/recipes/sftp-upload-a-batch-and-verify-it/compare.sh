#!/bin/sh
# Usage: sh compare.sh PORT DIR
# Lists /inbox/ on the server and compares every name and size with the files of DIR.
set -e
port=$1; dir=$2
pin=$(ssh-keygen -l -f host_key.pub | cut -d' ' -f2 | sed 's/^SHA256://')
for name in $(ls "$dir"); do echo "$name $(wc -c < "$dir/$name" | tr -d ' ')"; done | LC_ALL=C sort > local.txt
curl -s -S --hostpubsha256 "$pin" -u alice:s3cret "sftp://127.0.0.1:$port/inbox/" > listing.txt
awk '{print $NF, $5}' listing.txt | LC_ALL=C sort > remote.txt
if diff local.txt remote.txt > diff.txt; then
  echo "all $(wc -l < local.txt | tr -d ' ') files match"
  cat remote.txt
else
  echo "MISMATCH"
  cat diff.txt
  exit 1
fi
