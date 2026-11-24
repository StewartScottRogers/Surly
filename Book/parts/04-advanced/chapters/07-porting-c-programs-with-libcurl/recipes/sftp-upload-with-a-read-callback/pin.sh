#!/bin/sh
# Usage: sh pin.sh KEY.pub > pin.txt
ssh-keygen -l -f "$1" | cut -d' ' -f2 | sed 's/^SHA256://'
