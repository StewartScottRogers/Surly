#!/bin/sh
# pin.sh PORT OUT: fetches /report.bin over SFTP from the server on PORT into OUT, trusting only the
# host key whose fingerprint is in host_key.pub, so a different key is refused (exit 60).
pin=$(ssh-keygen -l -f host_key.pub | cut -d' ' -f2 | sed 's/^SHA256://')
curl -sS --hostpubsha256 "$pin" -u alice:s3cret -o "$2" "sftp://127.0.0.1:$1/report.bin"
