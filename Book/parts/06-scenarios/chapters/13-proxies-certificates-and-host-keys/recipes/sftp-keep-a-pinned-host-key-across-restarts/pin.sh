#!/bin/sh
# Usage: sh pin.sh KEY.pub > pin.cfg
fingerprint=$(ssh-keygen -l -f "$1" | cut -d' ' -f2 | sed 's/^SHA256://')
printf 'hostpubsha256 = "%s"\n' "$fingerprint"
